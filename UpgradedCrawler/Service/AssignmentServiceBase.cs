using UpgradedCrawler.Core.Data;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Helpers;

namespace UpgradedCrawler.Service;

public abstract class AssignmentServiceBase(IHttpClientFactory httpClientFactory, ILogging logging) : IAssignmentService
{
    protected readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    protected readonly ILogging _logging = logging;

    protected abstract string ProviderId { get; }

    protected abstract Task<IEnumerable<(string id, string url, string title, string description)>> FetchAssignmentsAsync();

    public async Task<ICollection<AssignmentAnnouncement>> GetAssignmentAnnouncementsAsync(AppDbContext dbContext)
    {
        var fetched = await FetchAssignmentsAsync();
        var newAssignments = new List<AssignmentAnnouncement>();
        var currentWebsiteIds = new HashSet<string>();

        foreach (var (id, url, title, description) in fetched)
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            currentWebsiteIds.Add(id);

            if (!dbContext.Assignments!.Any(r => r.AssignmentId == id && r.ProviderId == ProviderId))
                newAssignments.Add(new AssignmentAnnouncement(id, url, ProviderId, title, DateTime.Now, description));
        }

        AssignmentCleanupHelper.CleanupOldAssignments(dbContext, ProviderId, currentWebsiteIds, _logging);
        dbContext.Assignments!.AddRange(newAssignments);
        await dbContext.SaveChangesAsync();

        return newAssignments;
    }
}
