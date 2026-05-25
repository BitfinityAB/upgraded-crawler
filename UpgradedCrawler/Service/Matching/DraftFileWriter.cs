using System.Text;
using UpgradedCrawler.Core.Entities;

namespace UpgradedCrawler.Service.Matching;

public class DraftFileWriter(string draftsFolder)
{
    public void EnsureFolderStructure()
    {
        Directory.CreateDirectory(draftsFolder);
        Directory.CreateDirectory(Path.Combine(draftsFolder, "accepted"));
        Directory.CreateDirectory(Path.Combine(draftsFolder, "rejected"));
    }

    public async Task<string> WriteAsync(AssignmentAnnouncement announcement, AssignmentAnalysis analysis)
    {
        var date = analysis.AnalyzedAt.ToString("yyyy-MM-dd");
        var filename = $"{date}_{announcement.ProviderId}_{announcement.AssignmentId}.md";
        var path = Path.Combine(draftsFolder, filename);

        var content = $"""
            # {announcement.Title} — Score: {analysis.MatchScore}/100

            **URL:** {announcement.Url}
            **Provider:** {announcement.ProviderId}
            **Analyzed:** {analysis.AnalyzedAt:yyyy-MM-dd HH:mm}

            ## Why this matches
            {analysis.MatchReason}

            ---

            ## Cold Email (Kall e-post)

            {analysis.ColdEmailDraft}

            ---

            ## Cover Letter (Personligt brev)

            {analysis.CoverLetterDraft}
            """;

        await File.WriteAllTextAsync(path, content, Encoding.UTF8);
        return filename;
    }
}
