using UpgradedCrawler.Core.Entities;

namespace UpgradedCrawler.Core.Interfaces
{
    public interface IEmailService
    {
        Task SendEmail(string fromAddress, string fromName, string to, string subject, ICollection<AssignmentAnnouncement> assignments);
    }
}