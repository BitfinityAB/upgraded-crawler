using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using UpgradedCrawler.Core.Interfaces;

namespace UpgradedCrawler.Service;

public partial class AliantAssignmentService(IHttpClientFactory httpClientFactory, ILogging logging)
    : AssignmentServiceBase(httpClientFactory, logging)
{
    private const string BaseUrl = "https://aliant.recman.io";
    private const string CsrfTokenPattern = "name=\"csrf-token\"\\s+content=\"(?<token>[^\"]+)\"";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    protected override string ProviderId => "aliant";

    protected override async Task<IEnumerable<(string id, string url, string title, string description)>> FetchAssignmentsAsync()
    {
        var httpClient = _httpClientFactory.CreateClient();

        var csrfToken = await GetCsrfTokenAsync(httpClient);
        if (string.IsNullOrEmpty(csrfToken))
        {
            _logging.Log("Aliant: CSRF token not found.");
            return [];
        }

        var jobPosts = await GetJobPostsAsync(httpClient, csrfToken);
        if (jobPosts is null || jobPosts.Count == 0)
        {
            _logging.Log("Aliant: no assignments in API response.");
            return [];
        }

        var results = new List<(string, string, string, string)>();
        foreach (var post in jobPosts)
        {
            var id = post.AdId.ToString();
            var url = $"{BaseUrl}/jobs/{id}";
            var title = post.Name ?? "";
            var description = await GetJobDescriptionAsync(httpClient, csrfToken, id);
            results.Add((id, url, title, description));
        }
        return results;
    }

    private async Task<string> GetCsrfTokenAsync(HttpClient httpClient)
    {
        var response = await httpClient.GetAsync($"{BaseUrl}/jobs?sort=newest");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var match = CsrfTokenRegex().Match(html);
        return match.Success ? match.Groups["token"].Value : "";
    }

    private static async Task<List<AliantJobPost>?> GetJobPostsAsync(HttpClient httpClient, string csrfToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/jobs?sort=newest");
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var payload = JsonSerializer.Deserialize<AliantJobsResponse>(json, JsonOptions);
        return payload?.Data?.JobPosts;
    }

    private async Task<string> GetJobDescriptionAsync(HttpClient httpClient, string csrfToken, string id)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/job/{id}");
            request.Headers.Add("X-CSRF-TOKEN", csrfToken);
            var response = await httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logging.Log($"Aliant: HTTP {(int)response.StatusCode} fetching description for job {id}.");
                return "";
            }

            var json = await response.Content.ReadAsStringAsync();
            var payload = JsonSerializer.Deserialize<AliantJobDetailResponse>(json, JsonOptions);
            return payload?.Data?.Ad?.Body ?? "";
        }
        catch (Exception ex)
        {
            _logging.Log($"Aliant: failed to fetch description for job {id}: {ex.Message}");
            return "";
        }
    }

    [GeneratedRegex(CsrfTokenPattern)]
    private static partial Regex CsrfTokenRegex();
}

internal class AliantJobsResponse
{
    [JsonPropertyName("data")]
    public AliantJobsData? Data { get; set; }
}

internal class AliantJobsData
{
    [JsonPropertyName("job_posts")]
    public List<AliantJobPost>? JobPosts { get; set; }
}

internal class AliantJobPost
{
    [JsonPropertyName("AdID")]
    public int AdId { get; set; }

    [JsonPropertyName("Name")]
    public string? Name { get; set; }
}

internal class AliantJobDetailResponse
{
    [JsonPropertyName("data")]
    public AliantJobDetailData? Data { get; set; }
}

internal class AliantJobDetailData
{
    [JsonPropertyName("ad")]
    public AliantJobAd? Ad { get; set; }
}

internal class AliantJobAd
{
    [JsonPropertyName("Body")]
    public string? Body { get; set; }
}
