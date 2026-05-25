using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Core.Interfaces;

namespace UpgradedCrawler.Service.Matching;

public class ProfileLoader(MatchingOptions options, ILogging logging)
{
    public async Task<string> LoadAsync()
    {
        var parts = new List<string>();

        var cvPath = Path.Combine(options.ProfileFolder, options.CvFileName);
        if (File.Exists(cvPath))
        {
            var cvText = options.CvFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                ? ExtractPdfText(cvPath)
                : ExtractDocxText(cvPath);
            parts.Add($"=== CV ===\n{cvText}");
        }
        else
        {
            logging.Log($"ProfileLoader: CV file not found at '{cvPath}'.");
        }

        var liFolder = Path.Combine(options.ProfileFolder, options.LinkedInExportFolder);
        if (Directory.Exists(liFolder))
        {
            var liText = await ExtractLinkedInTextAsync(liFolder);
            if (!string.IsNullOrWhiteSpace(liText))
                parts.Add($"=== LinkedIn Profile ===\n{liText}");
        }
        else
        {
            logging.Log($"ProfileLoader: LinkedIn export folder not found at '{liFolder}'.");
        }

        var prefsPath = Path.Combine(options.ProfileFolder, "preferences.md");
        if (File.Exists(prefsPath))
        {
            var prefs = await File.ReadAllTextAsync(prefsPath);
            parts.Add($"=== Preferences ===\n{prefs}");
        }
        else
        {
            logging.Log($"ProfileLoader: preferences.md not found at '{prefsPath}'.");
        }

        return string.Join("\n\n", parts);
    }

    private static string ExtractDocxText(string path)
    {
        using var doc = WordprocessingDocument.Open(path, isEditable: false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return string.Empty;
        return string.Join("\n", body.Descendants<Paragraph>()
            .Select(p => p.InnerText)
            .Where(t => !string.IsNullOrWhiteSpace(t)));
    }

    private static string ExtractPdfText(string path)
    {
        using var pdf = PdfDocument.Open(path);
        var sb = new StringBuilder();
        foreach (var page in pdf.GetPages())
            sb.AppendLine(page.Text);
        return sb.ToString();
    }

    private static async Task<string> ExtractLinkedInTextAsync(string liFolder)
    {
        var sb = new StringBuilder();

        var profileCsv = Path.Combine(liFolder, "Profile.csv");
        if (File.Exists(profileCsv))
        {
            var lines = await File.ReadAllLinesAsync(profileCsv);
            if (lines.Length >= 2)
            {
                var headers = lines[0].Split(',');
                var values = lines[1].Split(',');
                var data = headers.Zip(values).ToDictionary(p => p.First.Trim(), p => p.Second.Trim());
                if (data.TryGetValue("Headline", out var headline) && !string.IsNullOrWhiteSpace(headline))
                    sb.AppendLine($"Headline: {headline}");
                if (data.TryGetValue("Summary", out var summary) && !string.IsNullOrWhiteSpace(summary))
                    sb.AppendLine($"Summary: {summary}");
            }
        }

        var positionsCsv = Path.Combine(liFolder, "Positions.csv");
        if (File.Exists(positionsCsv))
        {
            var lines = await File.ReadAllLinesAsync(positionsCsv);
            if (lines.Length >= 2)
            {
                sb.AppendLine("\nPositions:");
                var headers = lines[0].Split(',');
                int companyIdx = Array.IndexOf(headers, "Company Name");
                int titleIdx = Array.IndexOf(headers, "Title");
                int startIdx = Array.IndexOf(headers, "Started On");
                int endIdx = Array.IndexOf(headers, "Finished On");
                foreach (var line in lines.Skip(1))
                {
                    var v = line.Split(',');
                    var company = companyIdx >= 0 && companyIdx < v.Length ? v[companyIdx].Trim() : "";
                    var title = titleIdx >= 0 && titleIdx < v.Length ? v[titleIdx].Trim() : "";
                    var start = startIdx >= 0 && startIdx < v.Length ? v[startIdx].Trim() : "";
                    var end = endIdx >= 0 && endIdx < v.Length ? v[endIdx].Trim() : "";
                    sb.AppendLine($"  - {title} at {company} ({start}–{end})");
                }
            }
        }

        var skillsCsv = Path.Combine(liFolder, "Skills.csv");
        if (File.Exists(skillsCsv))
        {
            var lines = await File.ReadAllLinesAsync(skillsCsv);
            if (lines.Length >= 2)
            {
                var headers = lines[0].Split(',');
                int nameIdx = Array.IndexOf(headers, "Name");
                var skills = lines.Skip(1)
                    .Select(l => l.Split(','))
                    .Where(v => nameIdx >= 0 && nameIdx < v.Length)
                    .Select(v => v[nameIdx].Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
                if (skills.Count > 0)
                    sb.AppendLine($"\nSkills: {string.Join(", ", skills)}");
            }
        }

        return sb.ToString();
    }
}
