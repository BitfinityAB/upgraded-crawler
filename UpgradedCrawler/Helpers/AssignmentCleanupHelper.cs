using UpgradedCrawler.Core.Data;
using UpgradedCrawler.Core.Interfaces;

namespace UpgradedCrawler.Helpers
{
    public static class AssignmentCleanupHelper
    {
        /// <summary>
        /// Removes assignments that are 30+ days old and not present in the current website assignment IDs.
        /// </summary>
        /// <param name="dbContext">The database context</param>
        /// <param name="providerId">The provider identifier</param>
        /// <param name="currentWebsiteIds">Set of assignment IDs currently present on the website</param>
        /// <param name="logging">Logging interface for cleanup notifications</param>
        /// <returns>The number of assignments that were deleted</returns>
        public static int CleanupOldAssignments(
            AppDbContext dbContext, 
            string providerId, 
            HashSet<string> currentWebsiteIds, 
            ILogging logging)
        {
            var cutoffDate = DateTime.Now.AddDays(-30);
            var assignmentsToDelete = dbContext.Assignments
                .Where(a => a.ProviderId == providerId 
                           && a.CreatedAt <= cutoffDate 
                           && !currentWebsiteIds.Contains(a.AssignmentId))
                .ToList();

            var deletedCount = assignmentsToDelete.Count;
            if (deletedCount > 0)
            {
                dbContext.Assignments.RemoveRange(assignmentsToDelete);
                logging.Log($"Cleaned up {deletedCount} old assignments for provider {providerId}");
            }

            return deletedCount;
        }
    }
}
