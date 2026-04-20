using System.Text.Json;
using System.Text.Json.Serialization;
using UpgradedCrawler.Core.Data;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Helpers;

namespace UpgradedCrawler.Service
{
    public partial class TechRelationsAssignmentService(IHttpClientFactory httpClientFactory, ILogging logging) : IAssignmentService
    {
        private const string providerId = "techrelations";
        private const string apiUrl = "https://www.techrelations.se/api/getAssignments?perPage=60";
        
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
        
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogging _logging = logging;

        public async Task<ICollection<AssignmentAnnouncement>> GetAssignmentAnnouncementsAsync(AppDbContext dbContext)
        {
            var httpClient = _httpClientFactory.CreateClient();
            var newAssignments = new List<AssignmentAnnouncement>();
            
            var response = await httpClient.GetAsync(apiUrl);
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();

            // Parse JSON response (case-insensitive property matching)
            var assignments = JsonSerializer.Deserialize<List<TechRelationsAssignment>>(responseString, JsonOptions);

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
                    // Only process assignments where assigned is false
                    if (assignment.Acf?.Assigned != false) continue;
                    
                    var assignmentId = assignment.Id.ToString();
                    if (string.IsNullOrEmpty(assignmentId)) continue;

                    // Track current website IDs for cleanup
                    currentWebsiteIds.Add(assignmentId);

                    // Transform the URL: replace admin.techrelations.se/assignments with www.techrelations.se/konsultuppdrag
                    var url = assignment.Link?.Replace("https://admin.techrelations.se/assignments", "https://www.techrelations.se/konsultuppdrag") ?? "";
                    var title = assignment.Title?.Rendered ?? "";

                    if (!dbContext.Assignments.Any(r => r.AssignmentId == assignmentId && r.ProviderId == providerId))
                    {
                        newAssignments.Add(new AssignmentAnnouncement(assignmentId, url, providerId, title, DateTime.Now));
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

    // DTO for deserializing TechRelations API response
    internal class TechRelationsAssignment
    {
        public int Id { get; set; }
        
        [JsonPropertyName("link")]
        public string Link { get; set; }
        
        [JsonPropertyName("title")]
        public TechRelationsTitle Title { get; set; }
        
        [JsonPropertyName("acf")]
        public TechRelationsAcf Acf { get; set; }
    }

    internal class TechRelationsTitle
    {
        [JsonPropertyName("rendered")]
        public string Rendered { get; set; }
    }

    internal class TechRelationsAcf
    {
        [JsonPropertyName("assigned")]
        public bool Assigned { get; set; }
    }
}

