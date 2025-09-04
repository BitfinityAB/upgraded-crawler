using System.Text.RegularExpressions;
using HtmlAgilityPack;
using UpgradedCrawler.Core.Data;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Helpers;

namespace UpgradedCrawler.Service
{
    public class TeamPilotAssignmentService(IHttpClientFactory httpClientFactory, ILogging logging) : IAssignmentService
    {
        private const string providerId = "teampilot";
        private const string baseUrl = "https://app.teampilot.io";

        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogging _logging = logging;

        public async Task<ICollection<AssignmentAnnouncement>> GetAssignmentAnnouncementsAsync(AppDbContext dbContext)
        {
            var httpClient = _httpClientFactory.CreateClient();
            var newAssignments = new List<AssignmentAnnouncement>();
            var response = await httpClient.GetAsync($"{baseUrl}/jobs");
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();

            // Load HTML content directly into HtmlAgilityPack for parsing
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(responseString);

            // Extract and display table data
            var rows = htmlDoc.DocumentNode.SelectSingleNode("/html/body/div[1]/div[3]/div/div[1]");

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

                var href = row.SelectSingleNode("./div/div[2]/div[contains(@class, 'd-grid')]/a").Attributes["href"].Value;
                var url = baseUrl + href;
                var id = href.Split("/job/")[1];
                
                // Track current website IDs for cleanup
                currentWebsiteIds.Add(id);

                var title = row.SelectSingleNode("./div/div[2]/h5")?.InnerText.Trim() ?? "";
                if (!dbContext.Assignments.Any(r => r.Id == id && r.ProviderId == providerId))
                {
                    newAssignments.Add(new AssignmentAnnouncement(id, url, providerId, title, DateTime.Now));
                }
            }

            // Cleanup: Remove assignments that are 30+ days old and not on the website anymore
            AssignmentCleanupHelper.CleanupOldAssignments(dbContext, providerId, currentWebsiteIds, _logging);

            foreach (var assignment in newAssignments)
            {
                dbContext.Assignments.Add(assignment);
            }

            await dbContext.SaveChangesAsync();

            return newAssignments;
        }

    }
}