using UpgradedCrawler.Core.Data;
using UpgradedCrawler.Core.Entities;

namespace UpgradedCrawler.Core.Interfaces
{
    public interface IAssignmentService
    {
        Task<ICollection<AssignmentAnnouncement>> GetAssignmentAnnouncementsAsync(AppDbContext dbContext);
    }
}