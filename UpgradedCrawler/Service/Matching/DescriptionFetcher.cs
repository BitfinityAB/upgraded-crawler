using HtmlAgilityPack;
using UpgradedCrawler.Core.Interfaces;

namespace UpgradedCrawler.Service.Matching;

public class DescriptionFetcher(IHttpClientFactory httpClientFactory, ILogging logging)
{
    private const int MaxLength = 3000;

    public async Task<string> FetchAsync(string url)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                logging.Log($"DescriptionFetcher: HTTP {(int)response.StatusCode} for '{url}'.");
                return string.Empty;
            }

            var html = await response.Content.ReadAsStringAsync();
            return ExtractMainText(html);
        }
        catch (Exception ex)
        {
            logging.Log($"DescriptionFetcher: failed to fetch '{url}': {ex.Message}");
            return string.Empty;
        }
    }

    private static string ExtractMainText(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Prefer semantic tags; fall back to largest div by text length
        var candidates = new[] { "article", "main", "section" }
            .Select(tag => doc.DocumentNode.SelectSingleNode($"//{tag}"))
            .Where(n => n is not null)
            .ToList();

        HtmlNode? best = candidates.FirstOrDefault()
            ?? doc.DocumentNode
                  .SelectNodes("//div")
                  ?.OrderByDescending(n => n.InnerText.Length)
                  .FirstOrDefault();

        var text = (best ?? doc.DocumentNode).InnerText;
        text = System.Net.WebUtility.HtmlDecode(text);

        // Collapse whitespace
        text = string.Join("\n", text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0));

        return text.Length > MaxLength ? text[..MaxLength] : text;
    }
}
