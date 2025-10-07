using Newtonsoft.Json;

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

        public AssignmentAnnouncement()
        {
        }

        public AssignmentAnnouncement(string assignmentId, string url, string providerId, string title, DateTime createdAt)
        {
            AssignmentId = assignmentId;
            Url = url;
            ProviderId = providerId;
            Title = title;
            CreatedAt = createdAt;
        }
    }
}