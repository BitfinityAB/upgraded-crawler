using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations.Schema;

namespace UpgradedCrawler.Core.Entities
{
    public record AssignmentAnnouncement
    {
        [JsonIgnore]
        public int Id { get; init; }
        [JsonProperty("id")]
        public string AssignmentId { get; init; } = string.Empty;
        public string Url { get; init; } = string.Empty;
        public string ProviderId { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        // Not persisted — carried in-memory from crawl to Phase 2 analysis within the same run.
        // Descriptions for analyzed assignments are stored in AssignmentAnalysis.Description.
        [NotMapped]
        public string Description { get; init; } = string.Empty;

        public AssignmentAnnouncement()
        {
        }

        public AssignmentAnnouncement(string assignmentId, string url, string providerId, string title, DateTime createdAt, string description = "")
        {
            AssignmentId = assignmentId;
            Url = url;
            ProviderId = providerId;
            Title = title;
            CreatedAt = createdAt;
            Description = description;
        }
    }
}