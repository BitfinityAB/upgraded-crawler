namespace UpgradedCrawler.Service.Matching;

public interface IAiTextClient
{
    Task<string> CompleteAsync(string model, string system, string user, int maxTokens = 2000);
}
