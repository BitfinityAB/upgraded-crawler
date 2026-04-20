using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using UpgradedCrawler.Core.Interfaces;

namespace UpgradedCrawler.Service;

public partial class UpgradedAssignmentService(IHttpClientFactory httpClientFactory, ILogging logging)
    : AssignmentServiceBase(httpClientFactory, logging)
{
    private const string NoncePattern = @"var\s+bobz\s*=\s*\{\s*""nonce""\s*:\s*""(?<nonce>\w+)""";
    private const string WebsiteUrl = "https://upgraded.se/lediga-uppdrag/";
    private const string AdminUrl = "https://upgraded.se/wp-admin/admin-ajax.php";

    protected override string ProviderId => "upgraded";

    protected override async Task<IEnumerable<(string id, string url, string title)>> FetchAssignmentsAsync()
    {
        var nonce = await GetNonce();
        if (string.IsNullOrEmpty(nonce))
        {
            _logging.Log("Upgraded: nonce not found, skipping.");
            return [];
        }

        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/javascript, */*; q=0.01");
        httpClient.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");

        var formData = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("action", "do_filter_posts"),
            new KeyValuePair<string, string>("nonce", nonce),
            new KeyValuePair<string, string>("params[ort-term]", "alla-orter"),
            new KeyValuePair<string, string>("params[roll-term]", "alla-roller"),
            new KeyValuePair<string, string>("params[kund-term]", "alla-kunder"),
        ]);

        var response = await httpClient.PostAsync(AdminUrl, formData);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(responseString);
        var htmlContent = jsonDoc.RootElement.GetProperty("content").GetString() ?? string.Empty;

        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(htmlContent);

        var rows = htmlDoc.DocumentNode.SelectNodes("//table/tr[position()>1]");
        if (rows is null || rows.Count == 0)
        {
            _logging.Log("Upgraded: no data rows found.");
            return [];
        }

        var results = new List<(string, string, string)>();
        foreach (var row in rows)
        {
            var url = row.SelectSingleNode("td[1]/div[1]/div/div[1]/a")?.GetAttributeValue("href", "") ?? "";
            var title = row.SelectSingleNode("td[1]/div[2]/h5")?.InnerText.Trim() ?? "";
            var id = row.SelectSingleNode("td[1]/div[1]/div/div[2]/span[1]")?.InnerText.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(id))
            {
                _logging.Log($"Upgraded: failed to extract ID. URL: {url}, Title: {title}");
                continue;
            }
            results.Add((id, url, title));
        }
        return results;
    }

    private async Task<string> GetNonce()
    {
        var httpClient = _httpClientFactory.CreateClient();
        var response = await httpClient.GetAsync(WebsiteUrl);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var match = NonceRegex().Match(content);
        return match.Success && match.Groups["nonce"].Success ? match.Groups["nonce"].Value : string.Empty;
    }

    [GeneratedRegex(NoncePattern)]
    private static partial Regex NonceRegex();
}
