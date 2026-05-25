# AI Assignment Matching Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Phase 2 pipeline that scores new crawled assignments against the user's CV/LinkedIn profile, persists analyses to SQLite, saves Swedish cold-email + cover-letter drafts to a local folder, and sends a compact match-summary email.

**Architecture:** Phase 1 (crawl) is completely unchanged. Phase 2 runs immediately after Phase 1 and is gated by `MatchingOptions.Enabled = false` by default. AI calls use Claude Haiku for cheap title pre-filtering and Claude Sonnet for full per-assignment analysis and Swedish draft generation. A folder-based feedback loop (user drags files to `accepted/`/`rejected/` subfolders) is passed as few-shot context to both Claude calls.

**Tech Stack:** .NET 10, C# 13, EF Core 10 + SQLite, Anthropic.SDK (tghamm NuGet), DocumentFormat.OpenXml (DOCX), PdfPig (PDF), HtmlAgilityPack (already present), xUnit + NSubstitute (already present).

---

## File Map

**Create:**
| Path | Responsibility |
|---|---|
| `UpgradedCrawler.Core/Entities/AssignmentAnalysis.cs` | EF entity — analysis result per assignment |
| `UpgradedCrawler.Core/Entities/MatchingOptions.cs` | Config record |
| `UpgradedCrawler/Service/Matching/IAiTextClient.cs` | Thin interface over Anthropic client (for testability) |
| `UpgradedCrawler/Service/Matching/AnthropicTextClient.cs` | Real implementation wrapping Anthropic.SDK |
| `UpgradedCrawler/Service/Matching/ProfileLoader.cs` | Reads CV + LinkedIn CSVs + preferences.md → plain-text string |
| `UpgradedCrawler/Service/Matching/FeedbackLoader.cs` | Scans accepted/rejected folders → FeedbackEntry list |
| `UpgradedCrawler/Service/Matching/DescriptionFetcher.cs` | Secondary HTTP GET + HtmlAgilityPack for HTML-scraper providers |
| `UpgradedCrawler/Service/Matching/TitlePreFilter.cs` | One Haiku call → relevant indices |
| `UpgradedCrawler/Service/Matching/AssignmentAnalyzer.cs` | One Sonnet call per assignment → score + reason + Swedish drafts |
| `UpgradedCrawler/Service/Matching/AssignmentAnalysisRepository.cs` | EF read/write for AssignmentAnalyses table |
| `UpgradedCrawler/Service/Matching/DraftFileWriter.cs` | Writes .md draft files to DraftsFolder |
| `UpgradedCrawler/Service/Matching/MatchingEmailService.cs` | Composes and sends compact Mailgun plain-text email |
| `UpgradedCrawler/Service/Matching/MatchResult.cs` | Record: Announcement + Analysis + DraftFileName |
| `UpgradedCrawler.Tests/Matching/ProfileLoaderTests.cs` | Unit tests |
| `UpgradedCrawler.Tests/Matching/FeedbackLoaderTests.cs` | Unit tests |
| `UpgradedCrawler.Tests/Matching/DescriptionFetcherTests.cs` | Unit tests |
| `UpgradedCrawler.Tests/Matching/TitlePreFilterTests.cs` | Unit tests |
| `UpgradedCrawler.Tests/Matching/AssignmentAnalyzerTests.cs` | Unit tests |
| `UpgradedCrawler.Tests/Matching/DraftFileWriterTests.cs` | Unit tests |

**Modify:**
| Path | Change |
|---|---|
| `UpgradedCrawler.Core/Data/AppDbContext.cs` | Add `DbSet<AssignmentAnalysis>` + unique index in `OnModelCreating` |
| `UpgradedCrawler.Core/Entities/AssignmentAnnouncement.cs` | Add `[NotMapped] string Description` (flows description through Phase 1 to Phase 2 without DB change) |
| `UpgradedCrawler/Service/AssignmentServiceBase.cs` | Change tuple to `(string id, string url, string title, string description)` |
| `UpgradedCrawler/Service/MissPrymAssignmentService.cs` | Extend DTO with `Description`; pass through tuple |
| `UpgradedCrawler/Service/TechRelationsAssignmentService.cs` | Extend DTO with `content.rendered`; pass through tuple |
| `UpgradedCrawler/Service/UpgradedAssignmentService.cs` | Return empty description in tuple |
| `UpgradedCrawler/Service/AliantAssignmentService.cs` | Return empty description in tuple |
| `UpgradedCrawler/Service/TeamPilotAssignmentService.cs` | Return empty description in tuple |
| `UpgradedCrawler/Program.cs` | Wire Phase 2 after Phase 1 |
| `UpgradedCrawler/appsettings.json` | Add `Matching` section |
| `UpgradedCrawler/appsettings.local.template.json` | Add `Matching` section with placeholders |
| `.gitignore` | Add `profile/` |
| `UpgradedCrawler/UpgradedCrawler.csproj` | Add Anthropic.SDK, DocumentFormat.OpenXml, PdfPig |
| `UpgradedCrawler.Tests/Fixtures/missprym-assignments.json` | Add `Description` field |
| `UpgradedCrawler.Tests/Fixtures/techrelations-assignments.json` | Add `content.rendered` field |
| `UpgradedCrawler.Tests/ProviderTests/MissPrymAssignmentServiceTests.cs` | Assert Description field |
| `UpgradedCrawler.Tests/ProviderTests/TechRelationsAssignmentServiceTests.cs` | Assert Description field |

---

## Task 1: NuGet packages

**Files:**
- Modify: `UpgradedCrawler/UpgradedCrawler.csproj`

- [ ] **Step 1: Add packages**

```bash
cd UpgradedCrawler
dotnet add package Anthropic.SDK
dotnet add package DocumentFormat.OpenXml
dotnet add package PdfPig
```

- [ ] **Step 2: Verify build**

```bash
cd ..
dotnet build
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add UpgradedCrawler/UpgradedCrawler.csproj
git commit -m "chore: add Anthropic.SDK, DocumentFormat.OpenXml, PdfPig packages"
```

---

## Task 2: AssignmentAnalysis entity, MatchingOptions, EF migration

**Files:**
- Create: `UpgradedCrawler.Core/Entities/AssignmentAnalysis.cs`
- Create: `UpgradedCrawler.Core/Entities/MatchingOptions.cs`
- Modify: `UpgradedCrawler.Core/Data/AppDbContext.cs`

- [ ] **Step 1: Create AssignmentAnalysis entity**

`UpgradedCrawler.Core/Entities/AssignmentAnalysis.cs`:
```csharp
namespace UpgradedCrawler.Core.Entities;

public class AssignmentAnalysis
{
    public int Id { get; set; }
    public string AssignmentId { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MatchScore { get; set; }
    public string MatchReason { get; set; } = string.Empty;
    public string ColdEmailDraft { get; set; } = string.Empty;
    public string CoverLetterDraft { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; }
}
```

- [ ] **Step 2: Create MatchingOptions config record**

`UpgradedCrawler.Core/Entities/MatchingOptions.cs`:
```csharp
namespace UpgradedCrawler.Core.Entities;

public record MatchingOptions
{
    public bool Enabled { get; init; } = false;
    public int ScoreThreshold { get; init; } = 70;
    public string ProfileFolder { get; init; } = "profile";
    public string CvFileName { get; init; } = "cv.pdf";
    public string LinkedInExportFolder { get; init; } = "linkedin-export";
    public string DraftsFolder { get; init; } = string.Empty;
    public string AnthropicApiKey { get; init; } = string.Empty;
}
```

- [ ] **Step 3: Add DbSet and unique index to AppDbContext**

In `UpgradedCrawler.Core/Data/AppDbContext.cs`, add after the existing `Assignments` DbSet:
```csharp
public DbSet<AssignmentAnalysis>? AssignmentAnalyses { get; set; }
```

In `OnModelCreating`, after the existing `AssignmentAnnouncement` config block, add:
```csharp
modelBuilder.Entity<AssignmentAnalysis>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).ValueGeneratedOnAdd();
    entity.HasIndex(e => new { e.AssignmentId, e.ProviderId })
          .IsUnique()
          .HasDatabaseName("IX_AssignmentAnalyses_AssignmentId_ProviderId");
});
```

- [ ] **Step 4: Generate migration**

Run from the solution root:
```bash
dotnet ef migrations add AddAssignmentAnalysis --project UpgradedCrawler.Core --startup-project UpgradedCrawler
```

Expected: new migration files created in `UpgradedCrawler.Core/Migrations/`.

- [ ] **Step 5: Verify build and tests still pass**

```bash
dotnet build
dotnet test
```
Expected: Build succeeded, all existing tests pass (the SqliteTestFixture uses `Migrate()` so the new table is created in test DB automatically).

- [ ] **Step 6: Commit**

```bash
git add UpgradedCrawler.Core/Entities/AssignmentAnalysis.cs
git add UpgradedCrawler.Core/Entities/MatchingOptions.cs
git add UpgradedCrawler.Core/Data/AppDbContext.cs
git add UpgradedCrawler.Core/Migrations/
git commit -m "feat: add AssignmentAnalysis entity and MatchingOptions config"
```

---

## Task 3: Extend description through the provider tuple

**Goal:** Change the internal `FetchAssignmentsAsync` tuple from `(id, url, title)` to `(id, url, title, description)`. API providers (MissPrym, TechRelations) populate description from their existing API response. HTML scraper providers (Upgraded, Aliant, TeamPilot) return empty string. Add `[NotMapped] Description` to `AssignmentAnnouncement` so Phase 2 can read the description from the announcement object.

**Files:**
- Modify: `UpgradedCrawler.Core/Entities/AssignmentAnnouncement.cs`
- Modify: `UpgradedCrawler/Service/AssignmentServiceBase.cs`
- Modify: `UpgradedCrawler/Service/MissPrymAssignmentService.cs`
- Modify: `UpgradedCrawler/Service/TechRelationsAssignmentService.cs`
- Modify: `UpgradedCrawler/Service/UpgradedAssignmentService.cs`
- Modify: `UpgradedCrawler/Service/AliantAssignmentService.cs`
- Modify: `UpgradedCrawler/Service/TeamPilotAssignmentService.cs`
- Modify: `UpgradedCrawler.Tests/Fixtures/missprym-assignments.json`
- Modify: `UpgradedCrawler.Tests/Fixtures/techrelations-assignments.json`
- Modify: `UpgradedCrawler.Tests/ProviderTests/MissPrymAssignmentServiceTests.cs`
- Modify: `UpgradedCrawler.Tests/ProviderTests/TechRelationsAssignmentServiceTests.cs`

- [ ] **Step 1: Add NotMapped Description to AssignmentAnnouncement**

Add to the record after the existing properties and before the constructors:
```csharp
[System.ComponentModel.DataAnnotations.Schema.NotMapped]
public string Description { get; init; } = string.Empty;
```

Update the parameterized constructor to accept and set description:
```csharp
public AssignmentAnnouncement(string assignmentId, string url, string providerId, string title, DateTime createdAt, string description = "")
{
    AssignmentId = assignmentId;
    Url = url;
    ProviderId = providerId;
    Title = title;
    CreatedAt = createdAt;
    Description = description;
}
```

Add `using System.ComponentModel.DataAnnotations.Schema;` at the top (or use the fully qualified name inline as shown above).

- [ ] **Step 2: Change tuple in AssignmentServiceBase**

In `UpgradedCrawler/Service/AssignmentServiceBase.cs`:

Change the abstract method signature:
```csharp
protected abstract Task<IEnumerable<(string id, string url, string title, string description)>> FetchAssignmentsAsync();
```

Update `GetAssignmentAnnouncementsAsync` to destructure the new tuple:
```csharp
foreach (var (id, url, title, description) in fetched)
{
    if (string.IsNullOrWhiteSpace(id)) continue;
    currentWebsiteIds.Add(id);

    if (!dbContext.Assignments!.Any(r => r.AssignmentId == id && r.ProviderId == ProviderId))
        newAssignments.Add(new AssignmentAnnouncement(id, url, ProviderId, title, DateTime.Now, description));
}
```

- [ ] **Step 3: Update MissPrym — add Description to DTO and fixture**

In `UpgradedCrawler.Tests/Fixtures/missprym-assignments.json`, add a Description field:
```json
[
  {"Id": "mp-001", "Title": "MissPrym Test Assignment", "Description": "Detailed MissPrym description."}
]
```

In `MissPrymAssignment` (bottom of `MissPrymAssignmentService.cs`):
```csharp
internal class MissPrymAssignment
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
```

Change the `FetchAssignmentsAsync` return type and result list type, and pass description in the tuple:
```csharp
protected override async Task<IEnumerable<(string id, string url, string title, string description)>> FetchAssignmentsAsync()
{
    // ... existing HTTP call unchanged ...
    var results = new List<(string, string, string, string)>();
    foreach (var assignment in assignments)
    {
        if (string.IsNullOrEmpty(assignment.Id)) continue;
        var url = $"{BaseUrl}/job-posting/{assignment.Id}";
        results.Add((assignment.Id, url, assignment.Title ?? "", assignment.Description));
    }
    return results;
}
```

- [ ] **Step 4: Update TechRelations — add content.rendered to DTO and fixture**

In `UpgradedCrawler.Tests/Fixtures/techrelations-assignments.json`, add `content` to the unassigned entry:
```json
[
  {
    "id": 999,
    "link": "https://admin.techrelations.se/assignments/999",
    "title": {"rendered": "TechRelations Test Assignment"},
    "content": {"rendered": "Detailed TechRelations description."},
    "acf": {"assigned": false}
  },
  {
    "id": 998,
    "link": "https://admin.techrelations.se/assignments/998",
    "title": {"rendered": "Already Assigned"},
    "acf": {"assigned": true}
  }
]
```

Add these classes at the bottom of `TechRelationsAssignmentService.cs`:
```csharp
internal class TechRelationsContent
{
    [JsonPropertyName("rendered")]
    public string? Rendered { get; set; }
}
```

Add to `TechRelationsAssignment`:
```csharp
[JsonPropertyName("content")]
public TechRelationsContent? Content { get; set; }
```

Change the `FetchAssignmentsAsync` return type and pass description:
```csharp
protected override async Task<IEnumerable<(string id, string url, string title, string description)>> FetchAssignmentsAsync()
{
    // ... existing HTTP call unchanged ...
    var results = new List<(string, string, string, string)>();
    foreach (var assignment in assignments)
    {
        if (assignment.Acf?.Assigned != false) continue;
        var id = assignment.Id.ToString();
        var url = assignment.Link?.Replace(
            "https://admin.techrelations.se/assignments",
            "https://www.techrelations.se/konsultuppdrag") ?? "";
        var title = assignment.Title?.Rendered ?? "";
        var description = assignment.Content?.Rendered ?? "";
        results.Add((id, url, title, description));
    }
    return results;
}
```

- [ ] **Step 5: Update HTML scraper providers — return empty description**

For each of `UpgradedAssignmentService`, `AliantAssignmentService`, `TeamPilotAssignmentService`:

Change the return type declaration:
```csharp
protected override async Task<IEnumerable<(string id, string url, string title, string description)>> FetchAssignmentsAsync()
```

Change the results list type:
```csharp
var results = new List<(string, string, string, string)>();
```

Change each `results.Add(...)` call to append an empty description. For example in Upgraded:
```csharp
results.Add((id, url, title, ""));
```
Apply the same pattern in Aliant and TeamPilot.

- [ ] **Step 6: Update provider tests to assert Description**

In `MissPrymAssignmentServiceTests.cs`, add:
```csharp
Assert.Equal("Detailed MissPrym description.", result.First().Description);
```

In `TechRelationsAssignmentServiceTests.cs`, add:
```csharp
Assert.Equal("Detailed TechRelations description.", result.First().Description);
```

- [ ] **Step 7: Run all tests**

```bash
dotnet test
```
Expected: all tests pass.

- [ ] **Step 8: Commit**

```bash
git add UpgradedCrawler.Core/Entities/AssignmentAnnouncement.cs
git add UpgradedCrawler/Service/AssignmentServiceBase.cs
git add UpgradedCrawler/Service/MissPrymAssignmentService.cs
git add UpgradedCrawler/Service/TechRelationsAssignmentService.cs
git add UpgradedCrawler/Service/UpgradedAssignmentService.cs
git add UpgradedCrawler/Service/AliantAssignmentService.cs
git add UpgradedCrawler/Service/TeamPilotAssignmentService.cs
git add UpgradedCrawler.Tests/Fixtures/missprym-assignments.json
git add UpgradedCrawler.Tests/Fixtures/techrelations-assignments.json
git add UpgradedCrawler.Tests/ProviderTests/MissPrymAssignmentServiceTests.cs
git add UpgradedCrawler.Tests/ProviderTests/TechRelationsAssignmentServiceTests.cs
git commit -m "feat: extend assignment tuple with description field"
```

---

## Task 4: ProfileLoader

**Files:**
- Create: `UpgradedCrawler/Service/Matching/ProfileLoader.cs`
- Create: `UpgradedCrawler.Tests/Matching/ProfileLoaderTests.cs`

- [ ] **Step 1: Write the failing tests**

`UpgradedCrawler.Tests/Matching/ProfileLoaderTests.cs`:
```csharp
using NSubstitute;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Service.Matching;
using Xunit;

namespace UpgradedCrawler.Tests.Matching;

public class ProfileLoaderTests
{
    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public async Task LoadsPreferencesSection()
    {
        var dir = TempDir();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "preferences.md"), "I want remote work.");
            var opts = new MatchingOptions { ProfileFolder = dir, CvFileName = "cv.pdf" };
            var loader = new ProfileLoader(opts, Substitute.For<ILogging>());

            var result = await loader.LoadAsync();

            Assert.Contains("=== Preferences ===", result);
            Assert.Contains("I want remote work.", result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task MissingCv_LogsWarningAndContinues()
    {
        var dir = TempDir();
        try
        {
            var logging = Substitute.For<ILogging>();
            var opts = new MatchingOptions { ProfileFolder = dir, CvFileName = "cv.pdf" };
            var loader = new ProfileLoader(opts, logging);

            var result = await loader.LoadAsync();

            logging.Received().Log(Arg.Is<string>(s => s.Contains("cv.pdf")));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task LoadsLinkedInProfileCsv()
    {
        var dir = TempDir();
        try
        {
            var liDir = Path.Combine(dir, "linkedin-export");
            Directory.CreateDirectory(liDir);
            await File.WriteAllTextAsync(
                Path.Combine(liDir, "Profile.csv"),
                "First Name,Last Name,Headline,Summary\nJohn,Doe,Senior .NET Developer,Expert in C#");

            var opts = new MatchingOptions
            {
                ProfileFolder = dir,
                CvFileName = "cv.pdf",
                LinkedInExportFolder = "linkedin-export"
            };
            var loader = new ProfileLoader(opts, Substitute.For<ILogging>());

            var result = await loader.LoadAsync();

            Assert.Contains("=== LinkedIn Profile ===", result);
            Assert.Contains("Senior .NET Developer", result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
```

- [ ] **Step 2: Run tests to confirm they fail**

```bash
dotnet test --filter "ProfileLoaderTests"
```
Expected: compilation error — `ProfileLoader` does not exist.

- [ ] **Step 3: Implement ProfileLoader**

`UpgradedCrawler/Service/Matching/ProfileLoader.cs`:
```csharp
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
        return string.Join("\n", body.Descendants<Paragraph>().Select(p => p.InnerText).Where(t => !string.IsNullOrWhiteSpace(t)));
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
                if (data.TryGetValue("Headline", out var headline))
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
```

- [ ] **Step 4: Run tests**

```bash
dotnet test --filter "ProfileLoaderTests"
```
Expected: 3 tests pass.

- [ ] **Step 5: Commit**

```bash
git add UpgradedCrawler/Service/Matching/ProfileLoader.cs
git add UpgradedCrawler.Tests/Matching/ProfileLoaderTests.cs
git commit -m "feat: add ProfileLoader"
```

---

## Task 5: FeedbackLoader

**Files:**
- Create: `UpgradedCrawler/Service/Matching/FeedbackLoader.cs`
- Create: `UpgradedCrawler.Tests/Matching/FeedbackLoaderTests.cs`

- [ ] **Step 1: Write failing tests**

`UpgradedCrawler.Tests/Matching/FeedbackLoaderTests.cs`:
```csharp
using UpgradedCrawler.Service.Matching;
using Xunit;

namespace UpgradedCrawler.Tests.Matching;

public class FeedbackLoaderTests
{
    private static string TempDraftsDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(dir, "accepted"));
        Directory.CreateDirectory(Path.Combine(dir, "rejected"));
        return dir;
    }

    [Fact]
    public async Task LoadsFeedbackFromBothFolders()
    {
        var dir = TempDraftsDir();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(dir, "accepted", "2026-05-20_upgraded_JOB-001.md"),
                "# Senior .NET Developer — Score: 82/100\nContent.");
            await File.WriteAllTextAsync(
                Path.Combine(dir, "rejected", "2026-05-19_missprym_mp-007.md"),
                "# Fullstack on-site — Score: 71/100\nContent.");

            var loader = new FeedbackLoader(dir);
            var result = await loader.LoadAsync();

            Assert.Equal(2, result.Count);
            Assert.Contains(result, f => f.Verdict == "accepted" && f.Score == 82 && f.Title.Contains("Senior .NET"));
            Assert.Contains(result, f => f.Verdict == "rejected" && f.Score == 71);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task EmptyFolders_ReturnsEmptyList()
    {
        var dir = TempDraftsDir();
        try
        {
            var loader = new FeedbackLoader(dir);
            var result = await loader.LoadAsync();
            Assert.Empty(result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task ReturnsAtMost20Entries()
    {
        var dir = TempDraftsDir();
        try
        {
            for (int i = 0; i < 25; i++)
            {
                await File.WriteAllTextAsync(
                    Path.Combine(dir, "accepted", $"2026-05-{i:D2}_upgraded_JOB-{i:D3}.md"),
                    $"# Title {i} — Score: {50 + i}/100\nContent.");
            }

            var loader = new FeedbackLoader(dir);
            var result = await loader.LoadAsync();

            Assert.Equal(20, result.Count);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
```

- [ ] **Step 2: Run tests to confirm failure**

```bash
dotnet test --filter "FeedbackLoaderTests"
```
Expected: compilation error.

- [ ] **Step 3: Implement FeedbackLoader**

`UpgradedCrawler/Service/Matching/FeedbackLoader.cs`:
```csharp
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
```

- [ ] **Step 4: Run tests**

```bash
dotnet test --filter "FeedbackLoaderTests"
```
Expected: 3 tests pass.

- [ ] **Step 5: Commit**

```bash
git add UpgradedCrawler/Service/Matching/FeedbackLoader.cs
git add UpgradedCrawler.Tests/Matching/FeedbackLoaderTests.cs
git commit -m "feat: add FeedbackLoader"
```

---

## Task 6: IAiTextClient interface + AnthropicTextClient

**Files:**
- Create: `UpgradedCrawler/Service/Matching/IAiTextClient.cs`
- Create: `UpgradedCrawler/Service/Matching/AnthropicTextClient.cs`

No tests needed for the thin wrapper. Integration is verified via the real app run.

- [ ] **Step 1: Create IAiTextClient**

`UpgradedCrawler/Service/Matching/IAiTextClient.cs`:
```csharp
namespace UpgradedCrawler.Service.Matching;

public interface IAiTextClient
{
    Task<string> CompleteAsync(string model, string system, string user, int maxTokens = 2000);
}
```

- [ ] **Step 2: Create AnthropicTextClient**

`UpgradedCrawler/Service/Matching/AnthropicTextClient.cs`:
```csharp
using Anthropic.SDK;
using Anthropic.SDK.Messaging;

namespace UpgradedCrawler.Service.Matching;

public class AnthropicTextClient(string apiKey) : IAiTextClient
{
    private readonly AnthropicClient _client = new(apiKey);

    public async Task<string> CompleteAsync(string model, string system, string user, int maxTokens = 2000)
    {
        var parameters = new MessageParameters
        {
            Model = model,
            MaxTokens = maxTokens,
            System = [new SystemMessage(system)],
            Messages =
            [
                new Message { Role = RoleType.User, Content = [new TextContent { Text = user }] }
            ]
        };
        var response = await _client.Messages.GetClaudeMessageAsync(parameters);
        return response.Content.OfType<TextContent>().FirstOrDefault()?.Text ?? string.Empty;
    }
}
```

> **Note:** If the Anthropic.SDK version installed has a different API (e.g., different `Message`/`TextContent` namespaces), check the package's README on NuGet. The structure above matches Anthropic.SDK 3.x (tghamm). The key pattern: `new AnthropicClient(apiKey)`, `MessageParameters`, `GetClaudeMessageAsync`.

- [ ] **Step 3: Build**

```bash
dotnet build
```
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add UpgradedCrawler/Service/Matching/IAiTextClient.cs
git add UpgradedCrawler/Service/Matching/AnthropicTextClient.cs
git commit -m "feat: add IAiTextClient interface and AnthropicTextClient"
```

---

## Task 7: DescriptionFetcher

**Files:**
- Create: `UpgradedCrawler/Service/Matching/DescriptionFetcher.cs`
- Create: `UpgradedCrawler.Tests/Matching/DescriptionFetcherTests.cs`

- [ ] **Step 1: Write failing tests**

`UpgradedCrawler.Tests/Matching/DescriptionFetcherTests.cs`:
```csharp
using System.Net;
using NSubstitute;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Service.Matching;
using UpgradedCrawler.Tests.Infrastructure;
using Xunit;

namespace UpgradedCrawler.Tests.Matching;

public class DescriptionFetcherTests
{
    [Fact]
    public async Task ExtractsTextFromArticleTag()
    {
        const string html = "<html><body><article>This is the job description content here.</article></body></html>";
        var handler = new FakeHttpMessageHandler(html);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));

        var fetcher = new DescriptionFetcher(factory, Substitute.For<ILogging>());
        var result = await fetcher.FetchAsync("https://example.com/job/123");

        Assert.Contains("job description content", result);
        Assert.DoesNotContain("<article>", result);
    }

    [Fact]
    public async Task HttpError_ReturnsEmptyStringAndLogs()
    {
        var handler = new FakeHttpMessageHandler("", HttpStatusCode.InternalServerError);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));
        var logging = Substitute.For<ILogging>();

        var fetcher = new DescriptionFetcher(factory, logging);
        var result = await fetcher.FetchAsync("https://example.com/job/123");

        Assert.Equal(string.Empty, result);
        logging.Received().Log(Arg.Any<string>());
    }

    [Fact]
    public async Task TruncatesLongContent()
    {
        var longText = new string('x', 5000);
        var html = $"<html><body><article>{longText}</article></body></html>";
        var handler = new FakeHttpMessageHandler(html);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));

        var fetcher = new DescriptionFetcher(factory, Substitute.For<ILogging>());
        var result = await fetcher.FetchAsync("https://example.com/job/123");

        Assert.True(result.Length <= 3000);
    }
}
```

- [ ] **Step 2: Run to confirm failure**

```bash
dotnet test --filter "DescriptionFetcherTests"
```
Expected: compilation error.

- [ ] **Step 3: Implement DescriptionFetcher**

`UpgradedCrawler/Service/Matching/DescriptionFetcher.cs`:
```csharp
using HtmlAgilityPack;
using UpgradedCrawler.Core.Interfaces;

namespace UpgradedCrawler.Service.Matching;

public class DescriptionFetcher(IHttpClientFactory httpClientFactory, ILogging logging)
{
    private const int MaxLength = 3000;

    public async Task<string> FetchAsync(string url)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                logging.Log($"DescriptionFetcher: HTTP {(int)response.StatusCode} for {url}.");
                return string.Empty;
            }

            var html = await response.Content.ReadAsStringAsync();
            return ExtractMainText(html);
        }
        catch (Exception ex)
        {
            logging.Log($"DescriptionFetcher: failed to fetch '{url}': {ex.Message}");
            return string.Empty;
        }
    }

    private static string ExtractMainText(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Prefer semantic tags; fall back to largest div by text length
        var candidates = new[] { "article", "main", "section" }
            .Select(tag => doc.DocumentNode.SelectSingleNode($"//{tag}"))
            .Where(n => n is not null)
            .ToList();

        HtmlNode? best = candidates.FirstOrDefault()
            ?? doc.DocumentNode
                  .SelectNodes("//div")
                  ?.OrderByDescending(n => n.InnerText.Length)
                  .FirstOrDefault();

        var text = (best ?? doc.DocumentNode).InnerText;
        text = System.Net.WebUtility.HtmlDecode(text);

        // Collapse whitespace
        text = string.Join("\n", text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0));

        return text.Length > MaxLength ? text[..MaxLength] : text;
    }
}
```

- [ ] **Step 4: Run tests**

```bash
dotnet test --filter "DescriptionFetcherTests"
```
Expected: 3 tests pass.

- [ ] **Step 5: Commit**

```bash
git add UpgradedCrawler/Service/Matching/DescriptionFetcher.cs
git add UpgradedCrawler.Tests/Matching/DescriptionFetcherTests.cs
git commit -m "feat: add DescriptionFetcher"
```

---

## Task 8: TitlePreFilter

**Files:**
- Create: `UpgradedCrawler/Service/Matching/TitlePreFilter.cs`
- Create: `UpgradedCrawler.Tests/Matching/TitlePreFilterTests.cs`

- [ ] **Step 1: Write failing tests**

`UpgradedCrawler.Tests/Matching/TitlePreFilterTests.cs`:
```csharp
using NSubstitute;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Service.Matching;
using Xunit;

namespace UpgradedCrawler.Tests.Matching;

public class TitlePreFilterTests
{
    private static AssignmentAnnouncement A(string id, string title) =>
        new(id, $"https://example.com/{id}", "p", title, DateTime.Now);

    [Fact]
    public async Task ReturnsRelevantIndices()
    {
        var aiClient = Substitute.For<IAiTextClient>();
        aiClient.CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
                .Returns("""{"relevant": [0, 2]}""");

        var filter = new TitlePreFilter(aiClient, Substitute.For<ILogging>());
        var assignments = new List<AssignmentAnnouncement>
        {
            A("a", "Senior .NET Developer"),
            A("b", "Art Director"),
            A("c", "React Developer"),
        };

        var result = await filter.FilterAsync(assignments, []);

        Assert.Equal(new HashSet<int> { 0, 2 }, result);
    }

    [Fact]
    public async Task MalformedJson_IncludesAllAssignments()
    {
        var aiClient = Substitute.For<IAiTextClient>();
        aiClient.CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
                .Returns("not valid json");

        var filter = new TitlePreFilter(aiClient, Substitute.For<ILogging>());
        var assignments = new List<AssignmentAnnouncement> { A("a", "Title A"), A("b", "Title B") };

        var result = await filter.FilterAsync(assignments, []);

        Assert.Equal(new HashSet<int> { 0, 1 }, result);
    }

    [Fact]
    public async Task EmptyInput_ReturnsEmptySet()
    {
        var aiClient = Substitute.For<IAiTextClient>();
        var filter = new TitlePreFilter(aiClient, Substitute.For<ILogging>());

        var result = await filter.FilterAsync([], []);

        Assert.Empty(result);
        await aiClient.DidNotReceive().CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }

    [Fact]
    public async Task FeedbackAppendedToSystemPrompt()
    {
        var aiClient = Substitute.For<IAiTextClient>();
        aiClient.CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
                .Returns("""{"relevant": [0]}""");

        var filter = new TitlePreFilter(aiClient, Substitute.For<ILogging>());
        var feedback = new List<FeedbackEntry>
        {
            new("Senior .NET at Volvo", 82, "accepted"),
        };

        await filter.FilterAsync([A("a", "Senior Developer")], feedback);

        await aiClient.Received().CompleteAsync(
            Arg.Any<string>(),
            Arg.Is<string>(s => s.Contains("ACCEPTED") && s.Contains("Volvo")),
            Arg.Any<string>(),
            Arg.Any<int>());
    }
}
```

- [ ] **Step 2: Run to confirm failure**

```bash
dotnet test --filter "TitlePreFilterTests"
```
Expected: compilation error.

- [ ] **Step 3: Implement TitlePreFilter**

`UpgradedCrawler/Service/Matching/TitlePreFilter.cs`:
```csharp
using System.Text.Json;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Core.Interfaces;

namespace UpgradedCrawler.Service.Matching;

public class TitlePreFilter(IAiTextClient aiClient, ILogging logging)
{
    private const string BaseSystemPrompt = """
        You are a job relevance filter. The user is a senior .NET and fullstack developer
        (C#, ASP.NET Core, React, Azure, SQL). Return only the indices (0-based) of
        assignments that could plausibly be relevant — include anything technical or
        development-adjacent. Exclude obvious mismatches: art director, project manager
        (non-technical), nurse, teacher, driver, etc. Be inclusive when uncertain.
        Return JSON: {"relevant": [0, 2, 5]}
        """;

    public async Task<HashSet<int>> FilterAsync(
        IList<AssignmentAnnouncement> assignments,
        IReadOnlyList<FeedbackEntry> feedback)
    {
        if (assignments.Count == 0) return [];

        var systemPrompt = feedback.Count == 0
            ? BaseSystemPrompt
            : BuildPromptWithFeedback(feedback);

        var titleList = string.Join("\n", assignments.Select((a, i) => $"{i}. {a.Title}"));

        var json = await aiClient.CompleteAsync("claude-haiku-4-5-20251001", systemPrompt, titleList, maxTokens: 256);

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("relevant")
                      .EnumerateArray()
                      .Select(e => e.GetInt32())
                      .Where(i => i >= 0 && i < assignments.Count)
                      .ToHashSet();
        }
        catch (Exception ex)
        {
            logging.Log($"TitlePreFilter: could not parse response: {ex.Message}. Including all {assignments.Count} assignment(s).");
            return Enumerable.Range(0, assignments.Count).ToHashSet();
        }
    }

    private static string BuildPromptWithFeedback(IReadOnlyList<FeedbackEntry> feedback)
    {
        var lines = feedback.Select(f => $"- {f.Verdict.ToUpper()} ({f.Score}): \"{f.Title}\"");
        return $"{BaseSystemPrompt}\n\nRecent feedback (use to calibrate inclusivity):\n{string.Join("\n", lines)}";
    }
}
```

- [ ] **Step 4: Run tests**

```bash
dotnet test --filter "TitlePreFilterTests"
```
Expected: 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git add UpgradedCrawler/Service/Matching/TitlePreFilter.cs
git add UpgradedCrawler.Tests/Matching/TitlePreFilterTests.cs
git commit -m "feat: add TitlePreFilter"
```

---

## Task 9: AssignmentAnalyzer

**Files:**
- Create: `UpgradedCrawler/Service/Matching/AssignmentAnalyzer.cs`
- Create: `UpgradedCrawler/Service/Matching/AssignmentAnalysisRepository.cs`
- Create: `UpgradedCrawler.Tests/Matching/AssignmentAnalyzerTests.cs`

- [ ] **Step 1: Write failing tests**

`UpgradedCrawler.Tests/Matching/AssignmentAnalyzerTests.cs`:
```csharp
using NSubstitute;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Service.Matching;
using Xunit;

namespace UpgradedCrawler.Tests.Matching;

public class AssignmentAnalyzerTests
{
    private static AssignmentAnnouncement Ann(string id = "id-001") =>
        new(id, "https://example.com/job", "upgraded", "Senior .NET Developer", DateTime.Now);

    [Fact]
    public async Task ParsesValidJsonResponse()
    {
        var aiClient = Substitute.For<IAiTextClient>();
        const string json = """{"score": 85, "reason": "Good match.", "cold_email": "Hej!", "cover_letter": "Till er,"}""";
        aiClient.CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
                .Returns(json);

        var analyzer = new AssignmentAnalyzer(aiClient, Substitute.For<ILogging>());
        var result = await analyzer.AnalyzeAsync(Ann(), "Job description", "My profile", []);

        Assert.Equal(85, result.MatchScore);
        Assert.Equal("Good match.", result.MatchReason);
        Assert.Equal("Hej!", result.ColdEmailDraft);
        Assert.Equal("Till er,", result.CoverLetterDraft);
        Assert.Equal("id-001", result.AssignmentId);
        Assert.Equal("upgraded", result.ProviderId);
    }

    [Fact]
    public async Task MalformedJson_ReturnsScoreMinusOne()
    {
        var aiClient = Substitute.For<IAiTextClient>();
        aiClient.CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
                .Returns("not json");

        var logging = Substitute.For<ILogging>();
        var analyzer = new AssignmentAnalyzer(aiClient, logging);
        var result = await analyzer.AnalyzeAsync(Ann("id-002"), "Description", "Profile", []);

        Assert.Equal(-1, result.MatchScore);
        logging.Received().Log(Arg.Any<string>());
    }

    [Fact]
    public async Task FeedbackIncludedInUserMessage()
    {
        var aiClient = Substitute.For<IAiTextClient>();
        aiClient.CompleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
                .Returns("""{"score": 70, "reason": "OK", "cold_email": "E", "cover_letter": "C"}""");

        var analyzer = new AssignmentAnalyzer(aiClient, Substitute.For<ILogging>());
        var feedback = new List<FeedbackEntry> { new("Senior .NET at Volvo", 82, "accepted") };

        await analyzer.AnalyzeAsync(Ann(), "Description", "Profile", feedback);

        await aiClient.Received().CompleteAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<string>(u => u.Contains("ACCEPTED") && u.Contains("Volvo")),
            Arg.Any<int>());
    }
}
```

- [ ] **Step 2: Run to confirm failure**

```bash
dotnet test --filter "AssignmentAnalyzerTests"
```
Expected: compilation error.

- [ ] **Step 3: Implement AssignmentAnalyzer**

`UpgradedCrawler/Service/Matching/AssignmentAnalyzer.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Core.Interfaces;

namespace UpgradedCrawler.Service.Matching;

public class AssignmentAnalyzer(IAiTextClient aiClient, ILogging logging)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private const string SystemPrompt = """
        You are a career advisor helping a senior .NET/fullstack developer find consulting
        assignments in Sweden. You will receive the user's profile and an assignment description.
        Analyze the match and write application materials in Swedish.

        Return JSON:
        {
          "score": <integer 0-100>,
          "reason": "<2-3 sentences in English explaining the match score>",
          "cold_email": "<complete cold email in Swedish, addressed to the staffing company>",
          "cover_letter": "<complete cover letter (personligt brev) in Swedish>"
        }

        Score guide: 80-100 = strong match, 60-79 = decent match, below 60 = weak match.
        """;

    public async Task<AssignmentAnalysis> AnalyzeAsync(
        AssignmentAnnouncement announcement,
        string description,
        string profileText,
        IReadOnlyList<FeedbackEntry> feedback)
    {
        var feedbackSection = feedback.Count == 0 ? "" : BuildFeedbackSection(feedback);

        var userMessage = $"""
            === My Profile ===
            {profileText}

            === Assignment ===
            Title: {announcement.Title}
            URL: {announcement.Url}

            {description}
            {feedbackSection}
            """;

        var json = await aiClient.CompleteAsync("claude-sonnet-4-6", SystemPrompt, userMessage, maxTokens: 4000);

        try
        {
            var parsed = JsonSerializer.Deserialize<AnalysisResponse>(json, JsonOpts)!;
            return new AssignmentAnalysis
            {
                AssignmentId = announcement.AssignmentId,
                ProviderId = announcement.ProviderId,
                Description = description,
                MatchScore = parsed.Score,
                MatchReason = parsed.Reason ?? string.Empty,
                ColdEmailDraft = parsed.ColdEmail ?? string.Empty,
                CoverLetterDraft = parsed.CoverLetter ?? string.Empty,
                AnalyzedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            logging.Log($"AssignmentAnalyzer: failed to parse response for '{announcement.AssignmentId}': {ex.Message}");
            return new AssignmentAnalysis
            {
                AssignmentId = announcement.AssignmentId,
                ProviderId = announcement.ProviderId,
                Description = description,
                MatchScore = -1,
                MatchReason = "Analysis failed",
                AnalyzedAt = DateTime.UtcNow
            };
        }
    }

    private static string BuildFeedbackSection(IReadOnlyList<FeedbackEntry> feedback)
    {
        var lines = feedback.Select(f => $"- {f.Verdict.ToUpper()} (score {f.Score}): \"{f.Title}\"");
        return $"\n=== Your feedback on previous matches ===\n{string.Join("\n", lines)}\n\nUse this to calibrate your score for the current assignment.";
    }
}

internal class AnalysisResponse
{
    [JsonPropertyName("score")] public int Score { get; set; }
    [JsonPropertyName("reason")] public string? Reason { get; set; }
    [JsonPropertyName("cold_email")] public string? ColdEmail { get; set; }
    [JsonPropertyName("cover_letter")] public string? CoverLetter { get; set; }
}
```

- [ ] **Step 4: Implement AssignmentAnalysisRepository**

`UpgradedCrawler/Service/Matching/AssignmentAnalysisRepository.cs`:
```csharp
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
```

- [ ] **Step 5: Run tests**

```bash
dotnet test --filter "AssignmentAnalyzerTests"
```
Expected: 3 tests pass.

- [ ] **Step 6: Commit**

```bash
git add UpgradedCrawler/Service/Matching/AssignmentAnalyzer.cs
git add UpgradedCrawler/Service/Matching/AssignmentAnalysisRepository.cs
git add UpgradedCrawler.Tests/Matching/AssignmentAnalyzerTests.cs
git commit -m "feat: add AssignmentAnalyzer and AssignmentAnalysisRepository"
```

---

## Task 10: DraftFileWriter

**Files:**
- Create: `UpgradedCrawler/Service/Matching/DraftFileWriter.cs`
- Create: `UpgradedCrawler.Tests/Matching/DraftFileWriterTests.cs`

- [ ] **Step 1: Write failing tests**

`UpgradedCrawler.Tests/Matching/DraftFileWriterTests.cs`:
```csharp
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Service.Matching;
using Xunit;

namespace UpgradedCrawler.Tests.Matching;

public class DraftFileWriterTests
{
    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        return dir;
    }

    private static AssignmentAnnouncement Ann() =>
        new("JOB-001", "https://upgraded.se/job/JOB-001", "upgraded", "Senior .NET Developer", DateTime.Now);

    private static AssignmentAnalysis Analysis() => new()
    {
        AssignmentId = "JOB-001",
        ProviderId = "upgraded",
        MatchScore = 87,
        MatchReason = "Strong .NET match.",
        ColdEmailDraft = "Hej, jag är intresserad...",
        CoverLetterDraft = "Till er,",
        AnalyzedAt = new DateTime(2026, 5, 24, 14, 32, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task CreatesFileWithCorrectFilename()
    {
        var dir = TempDir();
        try
        {
            var writer = new DraftFileWriter(dir);
            writer.EnsureFolderStructure();
            var filename = await writer.WriteAsync(Ann(), Analysis());

            Assert.Equal("2026-05-24_upgraded_JOB-001.md", filename);
            Assert.True(File.Exists(Path.Combine(dir, filename)));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task FileContainsExpectedSections()
    {
        var dir = TempDir();
        try
        {
            var writer = new DraftFileWriter(dir);
            writer.EnsureFolderStructure();
            var filename = await writer.WriteAsync(Ann(), Analysis());
            var content = await File.ReadAllTextAsync(Path.Combine(dir, filename));

            Assert.Contains("Score: 87/100", content);
            Assert.Contains("Strong .NET match.", content);
            Assert.Contains("Hej, jag är intresserad...", content);
            Assert.Contains("Till er,", content);
            Assert.Contains("## Cold Email", content);
            Assert.Contains("## Cover Letter", content);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task EnsureFolderStructure_CreatesSubfolders()
    {
        var dir = TempDir();
        try
        {
            var writer = new DraftFileWriter(dir);
            writer.EnsureFolderStructure();

            Assert.True(Directory.Exists(Path.Combine(dir, "accepted")));
            Assert.True(Directory.Exists(Path.Combine(dir, "rejected")));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task OverwritesExistingFile()
    {
        var dir = TempDir();
        try
        {
            var writer = new DraftFileWriter(dir);
            writer.EnsureFolderStructure();

            var filename = await writer.WriteAsync(Ann(), Analysis());
            var updated = Analysis();
            // Simulate re-run by writing again
            var filename2 = await writer.WriteAsync(Ann(), updated);

            Assert.Equal(filename, filename2);
            Assert.Single(Directory.GetFiles(dir, "*.md"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
```

- [ ] **Step 2: Run to confirm failure**

```bash
dotnet test --filter "DraftFileWriterTests"
```
Expected: compilation error.

- [ ] **Step 3: Implement DraftFileWriter**

`UpgradedCrawler/Service/Matching/DraftFileWriter.cs`:
```csharp
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
```

- [ ] **Step 4: Run tests**

```bash
dotnet test --filter "DraftFileWriterTests"
```
Expected: 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git add UpgradedCrawler/Service/Matching/DraftFileWriter.cs
git add UpgradedCrawler.Tests/Matching/DraftFileWriterTests.cs
git commit -m "feat: add DraftFileWriter"
```

---

## Task 11: MatchingEmailService

**Files:**
- Create: `UpgradedCrawler/Service/Matching/MatchResult.cs`
- Create: `UpgradedCrawler/Service/Matching/MatchingEmailService.cs`

No unit tests: use the same exception-on-failure integration pattern as `MailgunServiceTests`.

- [ ] **Step 1: Create MatchResult record**

`UpgradedCrawler/Service/Matching/MatchResult.cs`:
```csharp
using UpgradedCrawler.Core.Entities;

namespace UpgradedCrawler.Service.Matching;

public record MatchResult(AssignmentAnnouncement Announcement, AssignmentAnalysis Analysis, string DraftFileName);
```

- [ ] **Step 2: Implement MatchingEmailService**

`UpgradedCrawler/Service/Matching/MatchingEmailService.cs`:
```csharp
using System.Text;
using Mailgun.Messages;
using Mailgun.Service;
using Microsoft.Extensions.Options;
using UpgradedCrawler.Core.Entities;

namespace UpgradedCrawler.Service.Matching;

public class MatchingEmailService(IOptions<MailgunOptions> mailgunOptions)
{
    private readonly MailgunOptions _opts = mailgunOptions.Value;

    public async Task SendAsync(ICollection<MatchResult> matches, string draftsFolder)
    {
        if (matches.Count == 0) return;

        var body = BuildBody(matches, draftsFolder);
        var mg = new MessageService(_opts.ApiKey, null, "api.eu.mailgun.net/v3");

        var message = new MessageBuilder()
            .AddToRecipient(new Recipient { Email = _opts.To })
            .SetSubject($"{matches.Count} strong assignment match(es) found")
            .SetFromAddress(new Recipient { Email = _opts.FromAddress, DisplayName = _opts.FromName })
            .SetTextBody(body)
            .GetMessage();

        var response = await mg.SendMessageAsync(_opts.Domain, message);
        if (response is null)
            throw new InvalidOperationException("Mailgun SendMessageAsync returned null.");
        response.EnsureSuccessStatusCode();
    }

    private static string BuildBody(ICollection<MatchResult> matches, string draftsFolder)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{matches.Count} strong assignment match(es) found — {DateTime.Now:yyyy-MM-dd}");
        sb.AppendLine();

        int i = 1;
        foreach (var m in matches.OrderByDescending(m => m.Analysis.MatchScore))
        {
            sb.AppendLine($"── Match {i++} — Score: {m.Analysis.MatchScore}/100 ──────────────────────────");
            sb.AppendLine($"Title:    {m.Announcement.Title}");
            sb.AppendLine($"Provider: {m.Announcement.ProviderId}");
            sb.AppendLine($"URL:      {m.Announcement.Url}");
            sb.AppendLine($"Why:      {m.Analysis.MatchReason}");
            sb.AppendLine($"Draft:    {m.DraftFileName}");
            sb.AppendLine();
        }

        sb.AppendLine($"Drafts saved to: {draftsFolder}");
        return sb.ToString();
    }
}
```

- [ ] **Step 3: Build**

```bash
dotnet build
```
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add UpgradedCrawler/Service/Matching/MatchResult.cs
git add UpgradedCrawler/Service/Matching/MatchingEmailService.cs
git commit -m "feat: add MatchingEmailService"
```

---

## Task 12: Wiring — Program.cs, config, .gitignore

**Files:**
- Modify: `UpgradedCrawler/Program.cs`
- Modify: `UpgradedCrawler/appsettings.json`
- Modify: `UpgradedCrawler/appsettings.local.template.json`
- Modify: `.gitignore`

- [ ] **Step 1: Add Matching section to appsettings.json**

Add after the existing `"Providers"` entry:
```json
"Matching": {
  "Enabled": false,
  "ScoreThreshold": 70,
  "ProfileFolder": "profile",
  "CvFileName": "cv.pdf",
  "LinkedInExportFolder": "linkedin-export",
  "DraftsFolder": "",
  "AnthropicApiKey": ""
}
```

- [ ] **Step 2: Add Matching section to appsettings.local.template.json**

Add after the existing `"Providers"` entry:
```json
"Matching": {
  "Enabled": false,
  "ScoreThreshold": 70,
  "ProfileFolder": "C:\\Users\\azimuth\\profile",
  "CvFileName": "cv.pdf",
  "LinkedInExportFolder": "linkedin-export",
  "DraftsFolder": "C:\\Users\\azimuth\\OneDrive\\AssignmentDrafts",
  "AnthropicApiKey": "<your-anthropic-api-key>"
}
```

- [ ] **Step 3: Add profile/ to .gitignore**

At the end of `.gitignore`, add:
```
profile/
```

- [ ] **Step 4: Wire Phase 2 in Program.cs**

Add these usings at the top of `Program.cs`:
```csharp
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Service.Matching;
```

In `ConfigureServices`, add after the existing `services.Configure<NotificationOptions>(...)` line:
```csharp
services.Configure<MatchingOptions>(context.Configuration.GetSection("Matching"));
```

In the startup validation block (after existing MissPrym validation), add:
```csharp
var matchingOpts = host.Services.GetRequiredService<IOptions<MatchingOptions>>().Value;
if (matchingOpts.Enabled)
{
    if (string.IsNullOrWhiteSpace(matchingOpts.AnthropicApiKey))
        throw new InvalidOperationException("Matching.AnthropicApiKey is not configured.");
    if (string.IsNullOrWhiteSpace(matchingOpts.DraftsFolder))
        throw new InvalidOperationException("Matching.DraftsFolder is not configured.");
    if (!Directory.Exists(matchingOpts.ProfileFolder))
        throw new InvalidOperationException($"Matching.ProfileFolder '{matchingOpts.ProfileFolder}' does not exist.");
}
```

Replace the early-return block and the Phase 1 email send with the following (restructured to allow Phase 2 to run):

Find this block:
```csharp
if (newAssignments.Count == 0)
{
    logger.Log("No new records found.");
    return;
}

var suffix = newAssignments.Count == 1 ? "" : "s";
await emailService.SendEmail(
    mailgunOpts.FromAddress,
    mailgunOpts.FromName,
    mailgunOpts.To,
    $"New Assignment Announcement{suffix} on Upgraded People",
    newAssignments);
logger.Log($"Sent email for {newAssignments.Count} new record{suffix}.");
```

Replace with:
```csharp
if (newAssignments.Count > 0)
{
    var suffix = newAssignments.Count == 1 ? "" : "s";
    await emailService.SendEmail(
        mailgunOpts.FromAddress,
        mailgunOpts.FromName,
        mailgunOpts.To,
        $"New Assignment Announcement{suffix} on Upgraded People",
        newAssignments);
    logger.Log($"Sent email for {newAssignments.Count} new record{suffix}.");
}
else
{
    logger.Log("No new records found.");
}

if (matchingOpts.Enabled && newAssignments.Count > 0)
{
    logger.Log($"Phase 2: analyzing {newAssignments.Count} new assignment(s)...");

    var aiClient = new AnthropicTextClient(matchingOpts.AnthropicApiKey);
    var profileLoader = new ProfileLoader(matchingOpts, logger);
    var feedbackLoader = new FeedbackLoader(matchingOpts.DraftsFolder);
    var descFetcher = new DescriptionFetcher(host.Services.GetRequiredService<IHttpClientFactory>(), logger);
    var titleFilter = new TitlePreFilter(aiClient, logger);
    var analyzer = new AssignmentAnalyzer(aiClient, logger);
    var draftWriter = new DraftFileWriter(matchingOpts.DraftsFolder);
    var matchingEmail = new MatchingEmailService(host.Services.GetRequiredService<IOptions<MailgunOptions>>());
    var analysisRepo = new AssignmentAnalysisRepository(db);

    draftWriter.EnsureFolderStructure();

    var profileText = await profileLoader.LoadAsync();
    var feedback = await feedbackLoader.LoadAsync();

    var unanalyzed = newAssignments
        .Where(a => !analysisRepo.IsAnalyzed(a.AssignmentId, a.ProviderId))
        .ToList();

    if (unanalyzed.Count == 0)
    {
        logger.Log("Phase 2: all new assignments already analyzed.");
    }
    else
    {
        logger.Log($"Phase 2: pre-filtering {unanalyzed.Count} title(s) via Haiku...");
        var relevantIndices = await titleFilter.FilterAsync(unanalyzed, feedback);

        // Persist filtered-out assignments immediately
        foreach (var (idx, ann) in unanalyzed.Select((a, i) => (i, a)))
        {
            if (relevantIndices.Contains(idx)) continue;
            await analysisRepo.SaveAsync(new AssignmentAnalysis
            {
                AssignmentId = ann.AssignmentId,
                ProviderId = ann.ProviderId,
                MatchScore = 0,
                MatchReason = "Filtered by title pre-screen",
                AnalyzedAt = DateTime.UtcNow
            });
        }

        var relevant = relevantIndices.Select(i => unanalyzed[i]).ToList();
        logger.Log($"Phase 2: {relevant.Count} assignment(s) passed title filter. Running Sonnet analysis...");

        var matchResults = new List<MatchResult>();

        foreach (var ann in relevant)
        {
            var description = string.IsNullOrEmpty(ann.Description)
                ? await descFetcher.FetchAsync(ann.Url)
                : ann.Description;

            logger.Log($"Phase 2: analyzing '{ann.Title}'...");
            var analysis = await analyzer.AnalyzeAsync(ann, description, profileText, feedback);
            await analysisRepo.SaveAsync(analysis);

            if (analysis.MatchScore >= 0)
            {
                var filename = await draftWriter.WriteAsync(ann, analysis);
                if (analysis.MatchScore >= matchingOpts.ScoreThreshold)
                    matchResults.Add(new MatchResult(ann, analysis, filename));
            }
        }

        if (matchResults.Count > 0)
        {
            await matchingEmail.SendAsync(matchResults, matchingOpts.DraftsFolder);
            logger.Log($"Phase 2: sent match email for {matchResults.Count} strong match(es).");
        }
        else
        {
            logger.Log("Phase 2: no strong matches above threshold.");
        }
    }
}
```

- [ ] **Step 5: Build and run all tests**

```bash
dotnet build
dotnet test
```
Expected: 0 errors, all tests pass.

- [ ] **Step 6: Commit**

```bash
git add UpgradedCrawler/Program.cs
git add UpgradedCrawler/appsettings.json
git add UpgradedCrawler/appsettings.local.template.json
git add .gitignore
git commit -m "feat: wire Phase 2 AI matching pipeline in Program.cs"
```

---

## Spec Coverage Check

| Spec requirement | Task |
|---|---|
| AssignmentAnalysis entity with unique (AssignmentId, ProviderId) index | Task 2 |
| MatchingOptions config record | Task 2 |
| EF migration for AssignmentAnalyses table | Task 2 |
| Extend API provider DTOs with description | Task 3 |
| FetchAssignmentsAsync tuple → (id, url, title, description) | Task 3 |
| ProfileLoader: DOCX/PDF CV + LinkedIn CSVs + preferences.md | Task 4 |
| FeedbackLoader: scans accepted/rejected, max 20 entries, reads first line | Task 5 |
| IAiTextClient for testability | Task 6 |
| AnthropicTextClient wrapping Anthropic.SDK | Task 6 |
| DescriptionFetcher: secondary HTTP GET, HtmlAgilityPack, 3000-char limit | Task 7 |
| TitlePreFilter: Haiku call, JSON response, fallback = include all | Task 8 |
| TitlePreFilter: feedback context in system prompt | Task 8 |
| AssignmentAnalyzer: Sonnet call, JSON response, MatchScore=-1 on failure | Task 9 |
| AssignmentAnalyzer: feedback section in user message | Task 9 |
| AssignmentAnalysisRepository: IsAnalyzed + SaveAsync | Task 9 |
| DraftFileWriter: YYYY-MM-DD_provider_id.md filename, correct sections | Task 10 |
| DraftFileWriter: EnsureFolderStructure creates accepted/ and rejected/ | Task 10 |
| MatchingEmailService: plain-text email, ordered by score desc | Task 11 |
| Program.cs: Phase 2 gated by Enabled flag | Task 12 |
| Startup config validation (API key, drafts folder, profile folder) | Task 12 |
| appsettings.json and template updated | Task 12 |
| profile/ in .gitignore | Task 12 |
| Score=0 for title-filtered-out assignments | Task 12 |
| DescriptionFetcher only called for empty-description (HTML scraper) assignments | Task 12 |
