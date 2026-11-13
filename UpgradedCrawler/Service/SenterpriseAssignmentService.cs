using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using UpgradedCrawler.Core.Interfaces;

namespace UpgradedCrawler.Service;

public partial class SenterpriseAssignmentService(IHttpClientFactory httpClientFactory, ILogging logging)
    : AssignmentServiceBase(httpClientFactory, logging)
{
    private const string ListUrl = "https://jobb.senterprise.se/jobs?query=&department=Konsultuppdrag";
    private const string TitlePrefix = "Konsultuppdrag |";

    protected override string ProviderId => "senterprise";

    protected override async Task<IEnumerable<(string id, string url, string title, string description)>> FetchAssignmentsAsync()
    {
        var httpClient = _httpClientFactory.CreateClient();
        var response = await httpClient.GetAsync(ListUrl);
        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();

        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(responseString);

        var links = htmlDoc.DocumentNode.SelectNodes("//ul[@id='jobs_list_container']/li//a[@href]");
        if (links is null)
        {
            _logging.Log("Senterprise: no job links found in jobs_list_container.");
            return [];
        }

        var results = new List<(string, string, string, string)>();
        foreach (var link in links)
        {
            var url = link.GetAttributeValue("href", "");
            var match = JobIdRegex().Match(url);
            if (!match.Success) continue;

            var title = WebUtility.HtmlDecode(link.InnerText).Trim();
            if (title.StartsWith(TitlePrefix, StringComparison.OrdinalIgnoreCase))
                title = title[TitlePrefix.Length..].Trim();

            results.Add((match.Groups[1].Value, url, title, ""));
        }
        return results;
    }

    [GeneratedRegex(@"/jobs/(\d+)")]
    private static partial Regex JobIdRegex();
}
