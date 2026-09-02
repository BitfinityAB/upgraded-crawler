using UpgradedCrawler.Core.Data;
using UpgradedCrawler.Core.Entities;

namespace UpgradedCrawler.Service.Matching;

public class AssignmentAnalysisRepository(AppDbContext dbContext)
{
    public bool IsAnalyzed(string assignmentId, string providerId) =>
        dbContext.AssignmentAnalyses!.Any(a => a.AssignmentId == assignmentId && a.ProviderId == providerId);

    public async Task SaveAsync(AssignmentAnalysis analysis)
    {
        dbContext.AssignmentAnalyses!.Add(analysis);
        await dbContext.SaveChangesAsync();
    }
}
