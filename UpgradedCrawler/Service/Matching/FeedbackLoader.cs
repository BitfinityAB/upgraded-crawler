using System.Text.RegularExpressions;

namespace UpgradedCrawler.Service.Matching;

public record FeedbackEntry(string Title, int Score, string Verdict);

public partial class FeedbackLoader(string draftsFolder)
{
    // Matches first line: # Some Title — Score: 82/100
    [GeneratedRegex(@"^# (?<title>.+?) — Score: (?<score>\d+)/100")]
    private static partial Regex FirstLineRegex();

    public async Task<IReadOnlyList<FeedbackEntry>> LoadAsync()
    {
        var entries = new List<(FeedbackEntry entry, DateTime modified)>();
        await LoadFromFolder(Path.Combine(draftsFolder, "accepted"), "accepted", entries);
        await LoadFromFolder(Path.Combine(draftsFolder, "rejected"), "rejected", entries);

        return entries
            .OrderByDescending(e => e.modified)
            .Take(20)
            .Select(e => e.entry)
            .ToList();
    }

    private static async Task LoadFromFolder(string folder, string verdict, List<(FeedbackEntry, DateTime)> entries)
    {
        if (!Directory.Exists(folder)) return;

        foreach (var file in Directory.GetFiles(folder, "*.md"))
        {
            var firstLine = await ReadFirstLineAsync(file);
            if (string.IsNullOrWhiteSpace(firstLine)) continue;

            var match = FirstLineRegex().Match(firstLine);
            if (!match.Success) continue;

            var title = match.Groups["title"].Value;
            var score = int.Parse(match.Groups["score"].Value);
            entries.Add((new FeedbackEntry(title, score, verdict), File.GetLastWriteTime(file)));
        }
    }

    private static async Task<string> ReadFirstLineAsync(string path)
    {
        using var reader = new StreamReader(path);
        return await reader.ReadLineAsync() ?? string.Empty;
    }
}
