namespace UpgradedCrawler.Core.Entities
{
    public record MailgunTemplateData
    {
        public bool MultipleAssignments { get; set; }
        public required ICollection<AssignmentAnnouncement> Assignments { get; set; }
    }
}