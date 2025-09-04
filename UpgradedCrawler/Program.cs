using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Service;
using UpgradedCrawler.Helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using UpgradedCrawler.Core.Data;
using UpgradedCrawler.Core.Interfaces;
using Microsoft.Extensions.Configuration;

var forceRun = args.Contains("-f") || args.Contains("--force");
var logToEventLog = args.Contains("-e") || args.Contains("--eventlog");
var logger = new Logging(logToEventLog);

try
{
    if (!forceRun && !IsWorkingHour())
    {
        logger.Log("It's not working hours. The program will exit.");
        return;
    }
    var host = Host.CreateDefaultBuilder(args)
           .ConfigureAppConfiguration((hostingContext, config) =>
           {
               // Add local settings file if it exists
               var localSettings = Path.Combine(AppContext.BaseDirectory, "appsettings.local.json");
               if (File.Exists(localSettings))
               {
                   config.AddJsonFile(localSettings, optional: true, reloadOnChange: true);
               }
           })
           .ConfigureServices((context, services) =>
           {
               // Register keyed services
               services.AddKeyedScoped<IAssignmentService, UpgradedAssignmentService>("upgraded");
               services.AddKeyedScoped<IAssignmentService, AliantAssignmentService>("aliant");
               services.AddKeyedScoped<IAssignmentService, TeamPilotAssignmentService>("teampilot");
               services.AddScoped<ILogging>(_ => new Logging(logToEventLog));
               services.AddScoped<IEmailService, MailgunService>();
               services.AddDbContext<AppDbContext>();

               services.AddHttpClient<IAssignmentService, UpgradedAssignmentService>();
               services.AddHttpClient<IAssignmentService, AliantAssignmentService>();
               services.AddHttpClient<IAssignmentService, TeamPilotAssignmentService>();

               services.Configure<MailgunOptions>(
                   context.Configuration.GetSection("mailgun"));
           })
           .Build();

    // Get service and run
    var emailService = host.Services.GetRequiredService<IEmailService>();
    var db = host.Services.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    var newAssignments = new List<AssignmentAnnouncement>();

    foreach (var provider in new string[] { "upgraded", "aliant", "teampilot" })
    {
        var assignmentService = host.Services.GetKeyedService<IAssignmentService>(provider);
        newAssignments.AddRange(await assignmentService.GetAssignmentAnnouncementsAsync(db));
    }

    if (newAssignments.Count == 0)
    {
        logger.Log("No new records found.");
        return;
    }
    else
    {
        var suffix = newAssignments.Count == 1 ? "" : "s";
        var mailgunOptions = host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<MailgunOptions>>().Value;
        await emailService.SendEmail(mailgunOptions.To, $"New Assignment Announcement{suffix} on Upgraded People", newAssignments);
        logger.Log($"Successfully sent email notification for {newAssignments.Count} new record{suffix}.");
    }
}
catch (Exception ex)
{
    logger.Log($"An error occurred: {ex.Message}");
}

/// <summary>
/// Checks if the current time is within working hours.
/// </summary>
static bool IsWorkingHour()
{
    var now = DateTime.Now;
    return now.DayOfWeek != DayOfWeek.Saturday && now.DayOfWeek != DayOfWeek.Sunday && now.Hour >= 8 && now.Hour < 17;
}