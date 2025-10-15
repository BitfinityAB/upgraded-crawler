using System.Text.Json;
using UpgradedCrawler.Core.Data;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Helpers;

namespace UpgradedCrawler.Service
{
    public partial class MissPrymAssignmentService(IHttpClientFactory httpClientFactory, ILogging logging) : IAssignmentService
    {
        private const string providerId = "missprym";
        private const string baseUrl = "https://hint.missprym.com";
        private const string apiUrl = "https://mint-webapi.azurewebsites.net/Assignments/PublicEnriched";
        private const string apiKey = "0d808f87-991f-48b8-9e1c-f937625a3ff7";
        
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
        
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogging _logging = logging;

        public async Task<ICollection<AssignmentAnnouncement>> GetAssignmentAnnouncementsAsync(AppDbContext dbContext)
        {
            var httpClient = _httpClientFactory.CreateClient();
            var newAssignments = new List<AssignmentAnnouncement>();
            
            // Create request with required headers
            var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("Origin", baseUrl);
            request.Headers.Add("Referer", $"{baseUrl}/");
            
            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();

            // Parse JSON response (case-insensitive property matching)
            var assignments = JsonSerializer.Deserialize<List<MissPrymAssignment>>(responseString, JsonOptions);

            if (assignments == null || assignments.Count == 0)
            {
                _logging.Log("No assignments found in API response.");
                return Array.Empty<AssignmentAnnouncement>();
            }

            // Collect current website assignment IDs while processing new assignments
            var currentWebsiteIds = new HashSet<string>();

            foreach (var assignment in assignments)
            {
                try
                {
                    if (string.IsNullOrEmpty(assignment.Id)) continue;

                    // Track current website IDs for cleanup
                    currentWebsiteIds.Add(assignment.Id);

                    // Build URL
                    var url = $"{baseUrl}/job-posting/{assignment.Id}";
                    var title = assignment.Title ?? "";

                    if (!dbContext.Assignments.Any(r => r.AssignmentId == assignment.Id && r.ProviderId == providerId))
                    {
                        newAssignments.Add(new AssignmentAnnouncement(assignment.Id, url, providerId, title, DateTime.Now));
                    }
                }
                catch (Exception ex)
                {
                    _logging.Log($"Error processing assignment: {ex.Message}");
                    continue;
                }
            }

            // Cleanup: Remove assignments that are 30+ days old and not on the website anymore
            AssignmentCleanupHelper.CleanupOldAssignments(dbContext, providerId, currentWebsiteIds, _logging);

            // Add new assignments
            foreach (var assignment in newAssignments)
            {
                dbContext.Assignments.Add(assignment);
            }

            await dbContext.SaveChangesAsync();

            return newAssignments;
        }
    }

    // DTO for deserializing Miss Prym API response
    internal class MissPrymAssignment
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
    }
}

