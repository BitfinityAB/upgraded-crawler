using UpgradedCrawler.Core.Entities;

namespace UpgradedCrawler.Core.Interfaces
{
    public interface IEmailService
    {
        Task SendEmail(string to, string subject, ICollection<AssignmentAnnouncement> assignments);
    }
}