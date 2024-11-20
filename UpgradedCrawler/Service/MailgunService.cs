using FluentAssertions;
using Mailgun.Messages;
using Mailgun.Service;
using Newtonsoft.Json.Linq;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Core.Extensions;
using UpgradedCrawler.Core.Interfaces;
namespace UpgradedCrawler.Service;

public class MailgunService(MailgunOptions mailgunOptions) : IEmailService
{
    private readonly MailgunOptions mailgunOptions = mailgunOptions;

    public async Task SendEmail(string to, string subject, List<AssignmentAnnouncement> assignments)
    {
        var mg = new MessageService(mailgunOptions.ApiKey, null, "api.eu.mailgun.net/v3");

        var message = new MessageBuilder()
             .AddToRecipient(new Recipient
             {
                 Email = to
             })
             .SetSubject(subject)
             .SetFromAddress(new Recipient { Email = "noreply@bitfinity.dev", DisplayName = "Bitfinity AB" })
             .SetTemplate(mailgunOptions.TemplateName, JObject.FromObject(GetMailgunTemplateData(assignments)))
             .GetMessage();

        var content = await mg.SendMessageAsync(mailgunOptions.Domain, message);
        content.Should().NotBeNull();
    }

    private static MailgunTemplateData GetMailgunTemplateData(List<AssignmentAnnouncement> assignments)
    {
        var data = new MailgunTemplateData
        {
            Assignments = assignments,
            MultipleAssignments = assignments.Count > 1
        };
        return data;
    }
}
