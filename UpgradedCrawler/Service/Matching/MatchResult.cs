using UpgradedCrawler.Core.Entities;

namespace UpgradedCrawler.Service.Matching;

public record MatchResult(AssignmentAnnouncement Announcement, AssignmentAnalysis Analysis, string DraftFileName);
