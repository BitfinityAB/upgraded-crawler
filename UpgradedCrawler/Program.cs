﻿using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Service;
using UpgradedCrawler.Helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using UpgradedCrawler.Core.Data;
using UpgradedCrawler.Core.Interfaces;

try
{
    if (!IsWorkingHour())
    {
        Logging.Log("It's not working hours. The program will exit.");
        return;
    }
    var host = Host.CreateDefaultBuilder(args)
           .ConfigureServices((context, services) =>
           {
               // Register keyed services
               services.AddKeyedScoped<IAssignmentService, UpgradedAssignmentService>("upgraded");
               services.AddKeyedScoped<IAssignmentService, AliantAssignmentService>("aliant");
               services.AddScoped<IEmailService, MailgunService>();
               services.AddDbContext<AppDbContext>();

               services.AddHttpClient<IAssignmentService, UpgradedAssignmentService>();
               services.AddHttpClient<IAssignmentService, AliantAssignmentService>();

               services.Configure<MailgunOptions>(
                   context.Configuration.GetSection("mailgun"));
           })
           .Build();

    // Get service and run    
    var emailService = host.Services.GetRequiredService<IEmailService>();
    var db = host.Services.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    var newAssignments = new List<AssignmentAnnouncement>();

    foreach (var provider in new string[] { "upgraded", "aliant" })
    {
        var assignmentService = host.Services.GetKeyedService<IAssignmentService>(provider);
        newAssignments.AddRange(await assignmentService.GetAssignmentAnnouncementsAsync(db));
    }

    if (newAssignments.Count == 0)
    {
        Logging.Log("No new records found.");
        return;
    }
    else
    {
        var suffix = newAssignments.Count == 1 ? "" : "s";
        await emailService.SendEmail("aziz.khakulov@bitfinity.dev", $"New Assignment Announcement{suffix} on Upgraded People", newAssignments);
        Logging.Log($"Successfully sent email notification for {newAssignments.Count} new record{suffix}.");
    }
}
catch (Exception ex)
{
    Logging.Log($"An error occurred: {ex.Message}");
}

/// <summary>
/// Checks if the current time is within working hours.
/// </summary>
static bool IsWorkingHour()
{
    var now = DateTime.Now;
    return now.DayOfWeek != DayOfWeek.Saturday && now.DayOfWeek != DayOfWeek.Sunday && now.Hour >= 8 && now.Hour < 17;
}