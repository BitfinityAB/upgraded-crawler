using HtmlAgilityPack;
using UpgradedCrawler.Core.Interfaces;

namespace UpgradedCrawler.Service;

public class UpgradedAssignmentService(IHttpClientFactory httpClientFactory, ILogging logging)
    : AssignmentServiceBase(httpClientFactory, logging)
{
    private const string WebsiteUrl = "https://upgraded.se/lediga-uppdrag/";

    protected override string ProviderId => "upgraded";

    protected override async Task<IEnumerable<(string id, string url, string title, string description)>> FetchAssignmentsAsync()
    {
        var httpClient = _httpClientFactory.CreateClient();
        var response = await httpClient.GetAsync(WebsiteUrl);
        response.EnsureSuccessStatusCode();

        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(await response.Content.ReadAsStringAsync());

        var cards = htmlDoc.DocumentNode.SelectNodes("//article[@data-uppdrag-card]");
        // Thrown rather than logged so a layout change shows up as a provider error instead of "0 new".
        if (cards is null || cards.Count == 0)
            throw new InvalidOperationException("no assignment cards found on the page; the site layout may have changed.");

        var results = new List<(string, string, string, string)>();
        foreach (var card in cards)
        {
            var link = card.SelectSingleNode(".//h2/a");
            var url = link?.GetAttributeValue("href", "") ?? "";
            var title = link?.InnerText.Trim() ?? "";
            // The reference number is the only all-digit span on a card.
            var id = card.SelectNodes(".//span")?
                .Select(s => s.InnerText.Trim())
                .FirstOrDefault(t => t.Length > 0 && t.All(char.IsAsciiDigit)) ?? "";

            if (string.IsNullOrWhiteSpace(id))
            {
                _logging.Log($"Upgraded: failed to extract ID. URL: {url}, Title: {title}");
                continue;
            }
            results.Add((id, url, title, ""));
        }

        if (results.Count == 0)
            throw new InvalidOperationException($"found {cards.Count} assignment card(s) but could not extract an ID from any; the site layout may have changed.");

        return results;
    }
}
