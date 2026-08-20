using Mailgun.Messages;
using Mailgun.Service;
using Microsoft.Extensions.Options;
using UpgradedCrawler.Core.Entities;

namespace UpgradedCrawler.Service.Matching;

public class MatchingEmailService(IOptions<MailgunOptions> mailgunOptions)
{
    private readonly MailgunOptions _mailgunOptions = mailgunOptions.Value;

    public async Task SendAsync(ICollection<MatchResult> matches, string draftsFolder)
    {
        if (matches.Count == 0)
            return;

        var mg = new MessageService(_mailgunOptions.ApiKey, null, "api.eu.mailgun.net/v3");

        var subject = $"{matches.Count} strong assignment match(es) found";
        var body = BuildPlainTextBody(matches, draftsFolder);

        var message = new MessageBuilder()
            .AddToRecipient(new Recipient { Email = _mailgunOptions.To })
            .SetSubject(subject)
            .SetFromAddress(new Recipient { Email = _mailgunOptions.FromAddress, DisplayName = _mailgunOptions.FromName })
            .SetTextBody(body)
            .GetMessage();

        var response = await mg.SendMessageAsync(_mailgunOptions.Domain, message);
        if (response is null)
            throw new InvalidOperationException("Mailgun SendMessageAsync returned null.");
        response.EnsureSuccessStatusCode();
    }

    private static string BuildPlainTextBody(ICollection<MatchResult> matches, string draftsFolder)
    {
        var lines = new List<string>();

        var sortedMatches = matches.OrderByDescending(m => m.Analysis.MatchScore).ToList();

        foreach (var match in sortedMatches)
        {
            lines.Add($"Title: {match.Announcement.Title} — Score: {match.Analysis.MatchScore}/100");
            lines.Add($"Provider: {match.Announcement.ProviderId}");
            lines.Add($"URL: {match.Announcement.Url}");
            lines.Add($"Why: {match.Analysis.MatchReason}");
            lines.Add($"Draft: {match.DraftFileName}");
            lines.Add("");
        }

        lines.Add($"Drafts saved to: {draftsFolder}");

        return string.Join(Environment.NewLine, lines);
    }
}
