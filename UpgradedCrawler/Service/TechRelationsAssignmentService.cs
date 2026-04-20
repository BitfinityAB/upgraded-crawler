using System.Text.Json;
using System.Text.Json.Serialization;
using UpgradedCrawler.Core.Interfaces;

namespace UpgradedCrawler.Service;

public class TechRelationsAssignmentService(IHttpClientFactory httpClientFactory, ILogging logging)
    : AssignmentServiceBase(httpClientFactory, logging)
{
    private const string ApiUrl = "https://www.techrelations.se/api/getAssignments?perPage=60";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    protected override string ProviderId => "techrelations";

    protected override async Task<IEnumerable<(string id, string url, string title)>> FetchAssignmentsAsync()
    {
        var httpClient = _httpClientFactory.CreateClient();
        var response = await httpClient.GetAsync(ApiUrl);
        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();

        var assignments = JsonSerializer.Deserialize<List<TechRelationsAssignment>>(responseString, JsonOptions);
        if (assignments is null || assignments.Count == 0)
        {
            _logging.Log("TechRelations: no assignments in API response.");
            return [];
        }

        var results = new List<(string, string, string)>();
        foreach (var assignment in assignments)
        {
            if (assignment.Acf?.Assigned != false) continue;

            var id = assignment.Id.ToString();
            var url = assignment.Link?.Replace(
                "https://admin.techrelations.se/assignments",
                "https://www.techrelations.se/konsultuppdrag") ?? "";
            var title = assignment.Title?.Rendered ?? "";
            results.Add((id, url, title));
        }
        return results;
    }
}

internal class TechRelationsAssignment
{
    public int Id { get; set; }

    [JsonPropertyName("link")]
    public string? Link { get; set; }

    [JsonPropertyName("title")]
    public TechRelationsTitle? Title { get; set; }

    [JsonPropertyName("acf")]
    public TechRelationsAcf? Acf { get; set; }
}

internal class TechRelationsTitle
{
    [JsonPropertyName("rendered")]
    public string? Rendered { get; set; }
}

internal class TechRelationsAcf
{
    [JsonPropertyName("assigned")]
    public bool Assigned { get; set; }
}
