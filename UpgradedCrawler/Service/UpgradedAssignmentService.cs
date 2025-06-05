using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using UpgradedCrawler.Core.Data;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Core.Interfaces;

namespace UpgradedCrawler.Service
{
    public class UpgradedAssignmentService(IHttpClientFactory httpClientFactory, ILogging logging) : IAssignmentService
    {
        private const string providerId = "upgraded";
        private const string websiteUrl = "https://upgraded.se/lediga-uppdrag/";
        private const string adminUrl = "https://upgraded.se/wp-admin/admin-ajax.php";
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogging _logging = logging;
        public async Task<ICollection<AssignmentAnnouncement>> GetAssignmentAnnouncementsAsync(AppDbContext dbContext)
        {
            var newAssignments = new List<AssignmentAnnouncement>();

            var nonce = await GetNonce();
            if (string.IsNullOrEmpty(nonce))
            {
                _logging.Log("Nonce not found. The program will exit.");
                return Array.Empty<AssignmentAnnouncement>();
            }
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/javascript, */*; q=0.01");
            httpClient.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");

            // Prepare the form data
            var formData = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("action", "do_filter_posts"),
                new KeyValuePair<string, string>("nonce", $"{nonce}"),
                new KeyValuePair<string, string>("params[ort-term]", "alla-orter"),
                new KeyValuePair<string, string>("params[roll-term]", "alla-roller"),
                new KeyValuePair<string, string>("params[kund-term]", "alla-kunder"),
            ]);

            // Send the POST request
            var response = await httpClient.PostAsync(adminUrl, formData);
            response.EnsureSuccessStatusCode();

            // Read the response as a string
            var responseString = await response.Content.ReadAsStringAsync();

            // Parse the JSON to get the HTML content
            var jsonDoc = JsonDocument.Parse(responseString);
            string htmlContent = jsonDoc.RootElement.GetProperty("content").GetString();

            // Load HTML content into HtmlAgilityPack for parsing
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);

            // Extract and display table data
            var rows = htmlDoc.DocumentNode.SelectNodes("//table/tr[position()>1]");

            if (rows?.Count == 0)
            {
                _logging.Log("No data rows found in the table.");
                return Array.Empty<AssignmentAnnouncement>();
            }

            rows?.ToList().ForEach(row =>
            {
                var url = row.SelectSingleNode("td[1]/div[1]/div/div[1]/a")?.GetAttributeValue("href", "") ?? "";
                var title = row.SelectSingleNode("td[1]/div[2]/h5")?.InnerText.Trim() ?? "";
                var id = row.SelectSingleNode("td[1]/div[1]/div/div[2]/span[1]").InnerText.Trim();
                if (!dbContext.Assignments.Any(r => r.Id == id && r.ProviderId == providerId))
                {
                    newAssignments.Add(new AssignmentAnnouncement(id, url, providerId, title, DateTime.Now));
                }
            });

            dbContext.Assignments.AddRange(newAssignments);
            await dbContext.SaveChangesAsync();

            return newAssignments;
        }

        /// <summary>
        /// Gets nonce from the website.
        /// </summary>
        private async Task<string> GetNonce()
        {
            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync(websiteUrl);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();

            // Define a regex pattern to match the nonce value
            var noncePattern = @"var\s+bobz\s*=\s*\{\s*""nonce""\s*:\s*""(?<nonce>\w+)""";
            var match = Regex.Match(content, noncePattern);

            if (match.Success && match.Groups["nonce"].Success)
            {
                return match.Groups["nonce"].Value;
            }
            else
            {
                _logging.Log("Nonce not found in the response.");
                return string.Empty;
            }
        }
    }
}