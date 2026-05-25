using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using UpgradedCrawler;
using UpgradedCrawler.Core.Data;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Helpers;
using UpgradedCrawler.Service;
using UpgradedCrawler.Service.Matching;

var forceRun = args.Contains("-f") || args.Contains("--force");
var logToEventLog = args.Contains("-e") || args.Contains("--eventlog");
var logger = new Logging(logToEventLog);

try
{
    if (!forceRun && !IsWorkingHour())
    {
        logger.Log("Not working hours. Exiting.");
        return;
    }

    var host = Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((_, config) =>
        {
            var localSettings = Path.Combine(AppContext.BaseDirectory, "appsettings.local.json");
            if (File.Exists(localSettings))
                config.AddJsonFile(localSettings, optional: true, reloadOnChange: true);
        })
        .ConfigureServices((context, services) =>
        {
            services.AddKeyedScoped<IAssignmentService, UpgradedAssignmentService>("upgraded");
            services.AddKeyedScoped<IAssignmentService, AliantAssignmentService>("aliant");
            services.AddKeyedScoped<IAssignmentService, TeamPilotAssignmentService>("teampilot");
            services.AddKeyedScoped<IAssignmentService, MissPrymAssignmentService>("missprym");
            services.AddKeyedScoped<IAssignmentService, TechRelationsAssignmentService>("techrelations");
            services.AddScoped<ILogging>(_ => new Logging(logToEventLog));
            services.AddScoped<IEmailService, MailgunService>();
            services.AddDbContext<AppDbContext>(options =>
            {
                var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                options.UseSqlite($"Data Source={Path.Join(folder, "assignments.db")}");
            });

            services.AddHttpClient<IAssignmentService, UpgradedAssignmentService>();
            services.AddHttpClient<IAssignmentService, AliantAssignmentService>();
            services.AddHttpClient<IAssignmentService, TeamPilotAssignmentService>();
            services.AddHttpClient<IAssignmentService, MissPrymAssignmentService>();
            services.AddHttpClient<IAssignmentService, TechRelationsAssignmentService>();

            services.Configure<MailgunOptions>(context.Configuration.GetSection("mailgun"));
            services.Configure<MissPrymOptions>(context.Configuration.GetSection("MissPrym"));
            services.Configure<NotificationOptions>(context.Configuration.GetSection("Notification"));
            services.Configure<MatchingOptions>(context.Configuration.GetSection("Matching"));
        })
        .Build();

    // Validate required configuration at startup
    var mailgunOpts = host.Services.GetRequiredService<IOptions<MailgunOptions>>().Value;
    if (string.IsNullOrWhiteSpace(mailgunOpts.ApiKey))
        throw new InvalidOperationException("Mailgun ApiKey is not configured in appsettings.");
    if (string.IsNullOrWhiteSpace(mailgunOpts.Domain))
        throw new InvalidOperationException("Mailgun Domain is not configured in appsettings.");

    var missPrymOpts = host.Services.GetRequiredService<IOptions<MissPrymOptions>>().Value;
    if (string.IsNullOrWhiteSpace(missPrymOpts.ApiKey))
        throw new InvalidOperationException("MissPrym ApiKey is not configured in appsettings.");

    var matchingOpts = host.Services.GetRequiredService<IOptions<MatchingOptions>>().Value;
    if (matchingOpts.Enabled)
    {
        if (string.IsNullOrWhiteSpace(matchingOpts.AnthropicApiKey))
            throw new InvalidOperationException("Matching.AnthropicApiKey is not configured.");
        if (string.IsNullOrWhiteSpace(matchingOpts.DraftsFolder))
            throw new InvalidOperationException("Matching.DraftsFolder is not configured.");
        if (!Directory.Exists(matchingOpts.ProfileFolder))
            throw new InvalidOperationException($"Matching.ProfileFolder '{matchingOpts.ProfileFolder}' does not exist.");
    }

    var db = host.Services.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();

    var providers = host.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()
        .GetSection("Providers").Get<string[]>() ?? [];

    var emailService = host.Services.GetRequiredService<IEmailService>();
    var newAssignments = new List<AssignmentAnnouncement>();

    foreach (var provider in providers)
    {
        var service = host.Services.GetKeyedService<IAssignmentService>(provider);
        if (service is null)
        {
            logger.Log($"Warning: unknown provider '{provider}' in config. Skipping.");
            continue;
        }

        logger.Log($"Fetching assignments from '{provider}'...");
        try
        {
            var found = await service.GetAssignmentAnnouncementsAsync(db);
            logger.Log($"'{provider}': {found.Count} new assignment(s).");
            newAssignments.AddRange(found);
        }
        catch (Exception ex)
        {
            logger.Log($"'{provider}' error: {ex.Message}");
        }
    }

    if (newAssignments.Count > 0)
    {
        var suffix = newAssignments.Count == 1 ? "" : "s";
        await emailService.SendEmail(
            mailgunOpts.FromAddress,
            mailgunOpts.FromName,
            mailgunOpts.To,
            $"New Assignment Announcement{suffix} on Upgraded People",
            newAssignments);
        logger.Log($"Sent email for {newAssignments.Count} new record{suffix}.");
    }
    else
    {
        logger.Log("No new records found.");
    }

    if (matchingOpts.Enabled && newAssignments.Count > 0)
    {
        logger.Log($"Phase 2: analyzing {newAssignments.Count} new assignment(s)...");

        var aiClient = new AnthropicTextClient(matchingOpts.AnthropicApiKey);
        var profileLoader = new ProfileLoader(matchingOpts, logger);
        var feedbackLoader = new FeedbackLoader(matchingOpts.DraftsFolder);
        var descFetcher = new DescriptionFetcher(host.Services.GetRequiredService<IHttpClientFactory>(), logger);
        var titleFilter = new TitlePreFilter(aiClient, logger);
        var analyzer = new AssignmentAnalyzer(aiClient, logger);
        var draftWriter = new DraftFileWriter(matchingOpts.DraftsFolder);
        var matchingEmail = new MatchingEmailService(host.Services.GetRequiredService<IOptions<MailgunOptions>>());
        var analysisRepo = new AssignmentAnalysisRepository(db);

        draftWriter.EnsureFolderStructure();

        var profileText = await profileLoader.LoadAsync();
        var feedback = await feedbackLoader.LoadAsync();

        var unanalyzed = newAssignments
            .Where(a => !analysisRepo.IsAnalyzed(a.AssignmentId, a.ProviderId))
            .ToList();

        if (unanalyzed.Count == 0)
        {
            logger.Log("Phase 2: all new assignments already analyzed.");
        }
        else
        {
            logger.Log($"Phase 2: pre-filtering {unanalyzed.Count} title(s) via Haiku...");
            var relevantIndices = await titleFilter.FilterAsync(unanalyzed, feedback);

            foreach (var (idx, ann) in unanalyzed.Select((a, i) => (i, a)))
            {
                if (relevantIndices.Contains(idx)) continue;
                await analysisRepo.SaveAsync(new AssignmentAnalysis
                {
                    AssignmentId = ann.AssignmentId,
                    ProviderId = ann.ProviderId,
                    MatchScore = 0,
                    MatchReason = "Filtered by title pre-screen",
                    AnalyzedAt = DateTime.UtcNow
                });
            }

            var relevant = relevantIndices.Select(i => unanalyzed[i]).ToList();
            logger.Log($"Phase 2: {relevant.Count} assignment(s) passed title filter. Running Sonnet analysis...");

            var matchResults = new List<MatchResult>();

            foreach (var ann in relevant)
            {
                var description = string.IsNullOrEmpty(ann.Description)
                    ? await descFetcher.FetchAsync(ann.Url)
                    : ann.Description;

                logger.Log($"Phase 2: analyzing '{ann.Title}'...");
                var analysis = await analyzer.AnalyzeAsync(ann, description, profileText, feedback);
                await analysisRepo.SaveAsync(analysis);

                var filename = await draftWriter.WriteAsync(ann, analysis);
                if (analysis.MatchScore >= matchingOpts.ScoreThreshold)
                    matchResults.Add(new MatchResult(ann, analysis, filename));
            }

            if (matchResults.Count > 0)
            {
                await matchingEmail.SendAsync(matchResults, matchingOpts.DraftsFolder);
                logger.Log($"Phase 2: sent match email for {matchResults.Count} strong match(es).");
            }
            else
            {
                logger.Log("Phase 2: no strong matches above threshold.");
            }
        }
    }

    var notificationOpts = host.Services.GetRequiredService<IOptions<NotificationOptions>>().Value;
    if (notificationOpts.Enabled && OperatingSystem.IsMacOS())
    {
        var notifSuffix = newAssignments.Count == 1 ? "" : "s";
        Notification.ShowMacNotification("New Assignments", $"{newAssignments.Count} new assignment{notifSuffix} found.");
    }
}
catch (Exception ex)
{
    logger.Log($"Error: {ex.Message}");
}

static bool IsWorkingHour()
{
    var now = DateTime.Now;
    return now.DayOfWeek != DayOfWeek.Saturday
        && now.DayOfWeek != DayOfWeek.Sunday
        && now.Hour >= 8
        && now.Hour < 17;
}
