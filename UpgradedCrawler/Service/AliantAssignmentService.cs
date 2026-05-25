using System.Text.RegularExpressions;
using HtmlAgilityPack;
using UpgradedCrawler.Core.Interfaces;

namespace UpgradedCrawler.Service;

public partial class AliantAssignmentService(IHttpClientFactory httpClientFactory, ILogging logging)
    : AssignmentServiceBase(httpClientFactory, logging)
{
    private const string BaseUrl = "https://aliant.recman.se";
    private const string JobIdPattern = @"job_id=(\d+)";

    protected override string ProviderId => "aliant";

    protected override async Task<IEnumerable<(string id, string url, string title, string description)>> FetchAssignmentsAsync()
    {
        var httpClient = _httpClientFactory.CreateClient();
        var response = await httpClient.GetAsync($"{BaseUrl}/index.php");
        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();

        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(responseString);

        var container = htmlDoc.DocumentNode.SelectSingleNode("//div[contains(@id, 'job-post-listing-box')]");
        if (container is null)
        {
            _logging.Log("Aliant: job listing container not found.");
            return [];
        }

        var results = new List<(string, string, string, string)>();
        foreach (var row in container.ChildNodes)
        {
            if (row.Name != "div") continue;

            var onclick = row.Attributes["onclick"]?.Value;
            if (onclick is null) continue;

            var match = JobIdRegex().Match(onclick);
            var id = match.Success ? match.Groups[1].Value : "";
            if (string.IsNullOrEmpty(id)) continue;

            var url = $"{BaseUrl}/job.php?job_id={id}";
            var title = row.SelectSingleNode("./div/table/tr/td[2]/span")?.InnerText.Trim() ?? "";
            results.Add((id, url, title, ""));
        }
        return results;
    }

    [GeneratedRegex(JobIdPattern)]
    private static partial Regex JobIdRegex();
}
