namespace UpgradedCrawler.Core.Entities;

public record MailgunOptions
{
    public required string ApiKey { get; set; }
    public required string Domain { get; set; }
    public required string TemplateName { get; set; }
}
