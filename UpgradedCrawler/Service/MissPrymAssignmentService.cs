using System.Text.Json;
using Microsoft.Extensions.Options;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Core.Interfaces;

namespace UpgradedCrawler.Service;

public class MissPrymAssignmentService(
    IHttpClientFactory httpClientFactory,
    ILogging logging,
    IOptions<MissPrymOptions> options)
    : AssignmentServiceBase(httpClientFactory, logging)
{
    private const string BaseUrl = "https://hint.missprym.com";
    private const string ApiUrl = "https://mint-webapi.azurewebsites.net/Assignments/PublicEnriched";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly string _apiKey = options.Value.ApiKey;

    protected override string ProviderId => "missprym";

    protected override async Task<IEnumerable<(string id, string url, string title)>> FetchAssignmentsAsync()
    {
        var httpClient = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, ApiUrl);
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("Origin", BaseUrl);
        request.Headers.Add("Referer", $"{BaseUrl}/");

        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();

        var assignments = JsonSerializer.Deserialize<List<MissPrymAssignment>>(responseString, JsonOptions);
        if (assignments is null || assignments.Count == 0)
        {
            _logging.Log("MissPrym: no assignments in API response.");
            return [];
        }

        var results = new List<(string, string, string)>();
        foreach (var assignment in assignments)
        {
            if (string.IsNullOrEmpty(assignment.Id)) continue;
            var url = $"{BaseUrl}/job-posting/{assignment.Id}";
            results.Add((assignment.Id, url, assignment.Title ?? ""));
        }
        return results;
    }
}

internal class MissPrymAssignment
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}
