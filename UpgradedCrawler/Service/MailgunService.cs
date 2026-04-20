using Mailgun.Messages;
using Mailgun.Service;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Extensions;

namespace UpgradedCrawler.Service;

public class MailgunService(IOptions<MailgunOptions> mailgunOptions) : IEmailService
{
    private readonly MailgunOptions _mailgunOptions = mailgunOptions.Value;

    public async Task SendEmail(string fromAddress, string fromName, string to, string subject, ICollection<AssignmentAnnouncement> assignments)
    {
        var mg = new MessageService(_mailgunOptions.ApiKey, null, "api.eu.mailgun.net/v3");

        var message = new MessageBuilder()
            .AddToRecipient(new Recipient { Email = to })
            .SetSubject(subject)
            .SetFromAddress(new Recipient { Email = fromAddress, DisplayName = fromName })
            .SetTemplate(_mailgunOptions.TemplateName, JObject.FromObject(GetMailgunTemplateData(assignments)))
            .GetMessage();

        var response = await mg.SendMessageAsync(_mailgunOptions.Domain, message);
        if (response is null)
            throw new InvalidOperationException("Mailgun SendMessageAsync returned null.");
        response.EnsureSuccessStatusCode();
    }

    private static MailgunTemplateData GetMailgunTemplateData(ICollection<AssignmentAnnouncement> assignments) =>
        new() { Assignments = assignments, MultipleAssignments = assignments.Count > 1 };
}
