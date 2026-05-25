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

    if (newAssignments.Count == 0)
    {
        logger.Log("No new records found.");
        return;
    }

    var suffix = newAssignments.Count == 1 ? "" : "s";
    await emailService.SendEmail(
        mailgunOpts.FromAddress,
        mailgunOpts.FromName,
        mailgunOpts.To,
        $"New Assignment Announcement{suffix} on Upgraded People",
        newAssignments);
    logger.Log($"Sent email for {newAssignments.Count} new record{suffix}.");

    var notificationOpts = host.Services.GetRequiredService<IOptions<NotificationOptions>>().Value;
    if (notificationOpts.Enabled && OperatingSystem.IsMacOS())
        Notification.ShowMacNotification("New Assignments", $"{newAssignments.Count} new assignment{suffix} found.");
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
