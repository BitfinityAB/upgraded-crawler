using HtmlAgilityPack;
using UpgradedCrawler.Core.Interfaces;

namespace UpgradedCrawler.Service;

public class TeamPilotAssignmentService(IHttpClientFactory httpClientFactory, ILogging logging)
    : AssignmentServiceBase(httpClientFactory, logging)
{
    private const string BaseUrl = "https://app.teampilot.io";

    protected override string ProviderId => "teampilot";

    protected override async Task<IEnumerable<(string id, string url, string title)>> FetchAssignmentsAsync()
    {
        var httpClient = _httpClientFactory.CreateClient();
        var response = await httpClient.GetAsync($"{BaseUrl}/jobs");
        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();

        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(responseString);

        var activeHeader = htmlDoc.DocumentNode.SelectSingleNode("//h3[contains(text(), 'Active Positions')]");
        if (activeHeader is null)
        {
            _logging.Log("TeamPilot: Active Positions header not found.");
            return [];
        }

        var rows = activeHeader.SelectSingleNode(
            "following-sibling::div[@class='row' and following-sibling::h3[contains(text(), 'Historical Positions')]][1]");
        if (rows is null)
        {
            _logging.Log("TeamPilot: no rows found under Active Positions.");
            return [];
        }

        var results = new List<(string, string, string)>();
        foreach (var row in rows.ChildNodes)
        {
            if (row.Name != "div") continue;

            var href = row.SelectSingleNode("./div/div[2]/div[contains(@class, 'd-grid')]/a")
                          ?.Attributes["href"]?.Value;
            if (href is null) continue;

            var id = href.Split("/job/").ElementAtOrDefault(1) ?? "";
            if (string.IsNullOrEmpty(id)) continue;

            var url = BaseUrl + href;
            var title = row.SelectSingleNode("./div/div[2]/h5")?.InnerText.Trim() ?? "";
            results.Add((id, url, title));
        }
        return results;
    }
}
