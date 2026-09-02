using Microsoft.Extensions.Options;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Service;
using Xunit;

namespace UpgradedCrawler.Tests;

public class MailgunServiceTests
{
    [Fact]
    public async Task SendEmail_WithInvalidDomain_ThrowsHttpRequestException()
    {
        var options = Options.Create(new MailgunOptions
        {
            ApiKey = "invalid-key",
            Domain = "invalid.domain.test",
            TemplateName = "test-template",
            To = "to@example.com",
            FromAddress = "from@example.com",
            FromName = "Test"
        });

        var service = new MailgunService(options);
        var assignments = new List<AssignmentAnnouncement>
        {
            new("id-1", "https://example.com", "upgraded", "Test", DateTime.Now)
        };

        await Assert.ThrowsAnyAsync<Exception>(() =>
            service.SendEmail("from@example.com", "Test", "to@example.com", "Subject", assignments));
    }
}
