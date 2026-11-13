using System.Text.Json;
using System.Text.Json.Serialization;
using UpgradedCrawler.Core.Interfaces;

namespace UpgradedCrawler.Service;

/// <summary>
/// TeamPilot's job board is a single-page app on portal.teampilot.io backed by a public JSON API.
/// </summary>
public class TeamPilotAssignmentService(IHttpClientFactory httpClientFactory, ILogging logging)
    : AssignmentServiceBase(httpClientFactory, logging)
{
    private const string ApiBaseUrl = "https://api.teampilot.io/api/public/jobs";
    private const string PortalJobsUrl = "https://portal.teampilot.io/jobs";
    private const int PageSize = 50;
    private const int MaxPages = 20;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    protected override string ProviderId => "teampilot";

    protected override async Task<IEnumerable<(string id, string url, string title, string description)>> FetchAssignmentsAsync()
    {
        var httpClient = _httpClientFactory.CreateClient();

        var jobs = await GetJobsAsync(httpClient);
        if (jobs.Count == 0)
        {
            _logging.Log("TeamPilot: no assignments in API response.");
            return [];
        }

        var results = new List<(string, string, string, string)>();
        foreach (var job in jobs)
        {
            if (string.IsNullOrWhiteSpace(job.Id)) continue;

            var slug = string.IsNullOrWhiteSpace(job.SlugOrId) ? job.Id : job.SlugOrId;
            var url = $"{PortalJobsUrl}/{slug}";
            var description = await GetJobDescriptionAsync(httpClient, slug);
            results.Add((job.Id, url, job.Title?.Trim() ?? "", description));
        }
        return results;
    }

    private static async Task<List<TeamPilotJob>> GetJobsAsync(HttpClient httpClient)
    {
        var jobs = new List<TeamPilotJob>();
        for (var page = 1; page <= MaxPages; page++)
        {
            var response = await httpClient.GetAsync($"{ApiBaseUrl}?page={page}&pageSize={PageSize}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var payload = JsonSerializer.Deserialize<TeamPilotJobsResponse>(json, JsonOptions);
            var items = payload?.Items ?? [];
            jobs.AddRange(items);

            if (items.Count == 0 || jobs.Count >= (payload?.TotalCount ?? 0)) break;
        }
        return jobs;
    }

    private async Task<string> GetJobDescriptionAsync(HttpClient httpClient, string slugOrId)
    {
        try
        {
            var response = await httpClient.GetAsync($"{ApiBaseUrl}/{Uri.EscapeDataString(slugOrId)}");
            if (!response.IsSuccessStatusCode)
            {
                _logging.Log($"TeamPilot: HTTP {(int)response.StatusCode} fetching description for job '{slugOrId}'.");
                return "";
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TeamPilotJobDetail>(json, JsonOptions)?.Description ?? "";
        }
        catch (Exception ex)
        {
            _logging.Log($"TeamPilot: failed to fetch description for job '{slugOrId}': {ex.Message}");
            return "";
        }
    }
}

internal class TeamPilotJobsResponse
{
    [JsonPropertyName("items")]
    public List<TeamPilotJob>? Items { get; set; }

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }
}

internal class TeamPilotJob
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("slugOrId")]
    public string? SlugOrId { get; set; }
}

internal class TeamPilotJobDetail
{
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}
