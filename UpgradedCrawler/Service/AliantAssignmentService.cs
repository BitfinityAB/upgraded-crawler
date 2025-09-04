using System.Text.RegularExpressions;
using HtmlAgilityPack;
using UpgradedCrawler.Core.Data;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Helpers;

namespace UpgradedCrawler.Service
{
    public partial class AliantAssignmentService(IHttpClientFactory httpClientFactory, ILogging logging) : IAssignmentService
    {
        private const string providerId = "aliant";
        private const string baseUrl = "https://aliant.recman.se";

        private const string jobIdPattern = @"job_id=(\d+)";
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogging _logging = logging;

        public async Task<ICollection<AssignmentAnnouncement>> GetAssignmentAnnouncementsAsync(AppDbContext dbContext)
        {
            var httpClient = _httpClientFactory.CreateClient();
            var newAssignments = new List<AssignmentAnnouncement>();
            var response = await httpClient.GetAsync($"{baseUrl}/index.php");
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();

            // Load HTML content directly into HtmlAgilityPack for parsing
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(responseString);

            // Extract and display table data
            var rows = htmlDoc.DocumentNode.SelectSingleNode("//div[contains(@id, 'job-post-listing-box')]");

            if (rows == null)
            {
                _logging.Log("No data rows found in the table.");
                return Array.Empty<AssignmentAnnouncement>();
            }

            // Collect current website assignment IDs while processing new assignments
            var currentWebsiteIds = new HashSet<string>();

            foreach (var row in rows.ChildNodes)
            {
                if (row.Name != "div") continue;
                var jobIdMatches = MyRegex().Match(row.Attributes["onclick"].Value);
                var id = jobIdMatches.Success ? jobIdMatches.Groups[1].Value : "";
                if (string.IsNullOrEmpty(id)) continue;

                // Track current website IDs for cleanup
                currentWebsiteIds.Add(id);

                var url = $"{baseUrl}/job.php?job_id={id}";
                var title = row.SelectSingleNode("./div/table/tr/td[2]/span")?.InnerText.Trim() ?? "";
                if (!dbContext.Assignments.Any(r => r.Id == id && r.ProviderId == providerId))
                {
                    newAssignments.Add(new AssignmentAnnouncement(id, url, providerId, title, DateTime.Now));
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

        [GeneratedRegex(jobIdPattern)]
        private static partial Regex MyRegex();
    }
}