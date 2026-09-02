# AI Assignment Matching Design

**Date:** 2026-05-16
**Project:** UpgradedCrawler
**Scope:** Two-phase AI pipeline that scores crawled assignments against user profile and generates Swedish cold email + cover letter drafts

---

## Context

UpgradedCrawler crawls 5 Swedish staffing platforms and notifies on new assignments. This extension adds a second phase that:
1. Filters new assignments by title relevance (fast, cheap)
2. Fetches full descriptions for plausible matches
3. Scores each match against the user's CV + LinkedIn profile + preferences
4. Saves Swedish cold email and cover letter drafts to a local folder (OneDrive-compatible)
5. Sends a compact summary email listing strong matches
6. Incorporates accepted/rejected feedback from previous runs to improve future scoring

---

## Implementation Sequence

1. Data model — new `AssignmentAnalysis` entity + EF migration
2. Profile ingestion — `ProfileLoader` reads CV, LinkedIn export, preferences
3. Feedback loader — `FeedbackLoader` scans accepted/rejected folders
4. Description fetching — extend API DTOs; secondary HTTP GET for HTML scrapers
5. Title pre-filter — single Claude Haiku call per run (with feedback context)
6. Full analysis — Claude Sonnet call per relevant assignment (with feedback context)
7. Draft file writer — saves `.md` files to `DraftsFolder`; creates `accepted/` and `rejected/` subfolders
8. Matching email — compact Mailgun email with match summaries
9. Wiring — `Program.cs`, config, `.gitignore`

---

## Architecture

The existing crawl (Phase 1) is completely unchanged. Phase 2 runs immediately after and is gated by `MatchingOptions.Enabled` (default `false`).

```
Program.cs
  ── Phase 1 (existing, unchanged) ──────────────────────────────
  foreach provider
    → GetAssignmentAnnouncementsAsync   (crawl + dedup + save)
  → send "new assignments" email

  ── Phase 2 (new, if MatchingOptions.Enabled) ───────────────────
  ProfileLoader          reads profile/ → plain-text profile string
  FeedbackLoader         scans accepted/ + rejected/ → few-shot feedback list
  TitlePreFilter         1 Claude Haiku call (+ feedback context) → relevant IDs
  DescriptionFetcher     fetch description for each relevant assignment
  AssignmentAnalyzer     1 Claude Sonnet call each (+ feedback context) → score + reason + drafts
  AssignmentAnalysisRepo persist to AssignmentAnalysis table
  DraftFileWriter        save .md files to DraftsFolder
  MatchingEmailService   send compact strong-matches email (score >= threshold)
```

---

## New Files

| Path | Purpose |
|---|---|
| `UpgradedCrawler.Core/Entities/AssignmentAnalysis.cs` | EF entity |
| `UpgradedCrawler.Core/Entities/MatchingOptions.cs` | Config record |
| `UpgradedCrawler/Service/Matching/ProfileLoader.cs` | Reads and extracts profile text |
| `UpgradedCrawler/Service/Matching/FeedbackLoader.cs` | Scans accepted/rejected folders → feedback list |
| `UpgradedCrawler/Service/Matching/TitlePreFilter.cs` | Claude Haiku title relevance filter |
| `UpgradedCrawler/Service/Matching/DescriptionFetcher.cs` | Fetches full assignment descriptions |
| `UpgradedCrawler/Service/Matching/AssignmentAnalyzer.cs` | Claude Sonnet full analysis + drafts |
| `UpgradedCrawler/Service/Matching/DraftFileWriter.cs` | Writes .md draft files to DraftsFolder |
| `UpgradedCrawler/Service/Matching/MatchingEmailService.cs` | Composes and sends compact match email |
| `profile/preferences.md` | User's role/location/contract preferences (gitignored) |

## Modified Files

| Path | Change |
|---|---|
| `UpgradedCrawler.Core/Data/AppDbContext.cs` | Add `DbSet<AssignmentAnalysis>` |
| `UpgradedCrawler/Service/MissPrymAssignmentService.cs` | Extend DTO with description field |
| `UpgradedCrawler/Service/TechRelationsAssignmentService.cs` | Extend DTO with `content.rendered` |
| `UpgradedCrawler/Service/AssignmentServiceBase.cs` | Change tuple to `(id, url, title, description)` |
| `UpgradedCrawler/Program.cs` | Wire Phase 2 after Phase 1 |
| `UpgradedCrawler/appsettings.json` | Add `Matching` section |
| `UpgradedCrawler/appsettings.local.template.json` | Add `Matching` section with placeholders |
| `.gitignore` | Add `profile/` |

---

## Data Model

### AssignmentAnalysis entity

```csharp
namespace UpgradedCrawler.Core.Entities;

public class AssignmentAnalysis
{
    public int Id { get; set; }
    public string AssignmentId { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MatchScore { get; set; }          // 0–100; 0 = filtered out by title
    public string MatchReason { get; set; } = string.Empty;
    public string ColdEmailDraft { get; set; } = string.Empty;
    public string CoverLetterDraft { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; }
}
```

Unique index on `(AssignmentId, ProviderId)` — same composite pattern as `Assignments`. Once analyzed, an assignment is never re-processed.

### MatchingOptions config record

```csharp
public record MatchingOptions
{
    public bool Enabled { get; init; } = false;
    public int ScoreThreshold { get; init; } = 70;
    public string ProfileFolder { get; init; } = "profile";
    public string CvFileName { get; init; } = "cv.pdf";
    public string LinkedInExportFolder { get; init; } = "linkedin-export";  // extracted ZIP folder
    public string DraftsFolder { get; init; } = "";   // required when Enabled = true
    public string AnthropicApiKey { get; init; } = string.Empty;
}
```

### appsettings.json addition

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

---

## Profile Ingestion

`ProfileLoader` reads three sources from `ProfileFolder` and returns a single concatenated plain-text string used as Claude context:

1. **CV** (`CvFileName`) — DOCX or PDF. Use `DocumentFormat.OpenXml` for DOCX; `PdfPig` for PDF. Extracts paragraphs as plain text.
2. **LinkedIn export** (`LinkedInFileName`) — LinkedIn's data export is a ZIP archive. The user extracts it manually; `LinkedInFileName` points at the extracted folder. `ProfileLoader` reads `Profile.csv` for summary/headline, `Positions.csv` for work history, and `Skills.csv` for skills. Produces a compact structured summary.
3. **preferences.md** — Read as-is. Contains user-written preferences (role type, location, remote/on-site, industries, contract length, rate expectations etc.).

Output format:
```
=== CV ===
[extracted CV text]

=== LinkedIn Profile ===
[structured summary]

=== Preferences ===
[preferences.md content]
```

If any file is missing, log a warning and continue with what's available.

---

## Description Fetching

`FetchAssignmentsAsync` return type changes from `(string id, string url, string title)` to `(string id, string url, string title, string description)`.

### API-based providers (MissPrym, TechRelations)

Extend internal DTOs to capture description fields already present in the API response:

- **MissPrym** (`PublicEnriched` endpoint): add `Description` field to `MissPrymAssignment`
- **TechRelations** (WordPress API): add `content.rendered` via `TechRelationsContent` class; map to `description` in the tuple

### HTML scraper providers (Upgraded, Aliant, TeamPilot)

The listing page does not contain descriptions. `DescriptionFetcher` does a secondary HTTP GET to the individual assignment URL **only for assignments that pass the title pre-filter**. It:
1. GETs the URL
2. Uses HtmlAgilityPack to extract the main content block (heuristic: largest `<div>` or `<article>` block by text length)
3. Strips HTML tags to plain text
4. Truncates to 3000 characters to keep Claude prompts bounded

If the page is behind authentication, JS-rendered, or times out, description is stored as empty string. Claude falls back to title-only analysis.

---

## Claude API Integration

NuGet: `Anthropic.SDK` (latest stable).

### TitlePreFilter

**Model:** `claude-haiku-4-5-20251001` (fast, cheap — classification only)

**System prompt:**
```
You are a job relevance filter. The user is a senior .NET and fullstack developer
(C#, ASP.NET Core, React, Azure, SQL). Return only the indices (0-based) of
assignments that could plausibly be relevant — include anything technical or
development-adjacent. Exclude obvious mismatches: art director, project manager
(non-technical), nurse, teacher, driver, etc. Be inclusive when uncertain.
Return JSON: {"relevant": [0, 2, 5]}
```

**User message:** numbered list of assignment titles

**Handling:** assignments not in `relevant` list get an `AssignmentAnalysis` record with `MatchScore = 0`, empty drafts, `MatchReason = "Filtered by title pre-screen"`.

### AssignmentAnalyzer

**Model:** `claude-sonnet-4-6` (quality writing in Swedish)

**System prompt:**
```
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
```

**User message:**
```
=== My Profile ===
{profileText}

=== Assignment ===
Title: {title}
URL: {url}

{description}
```

**Error handling:** if the API call fails or returns malformed JSON, store `MatchScore = -1`, log the error, continue to next assignment.

---

## Feedback Loop

### Folder structure

```
DraftsFolder/
  2026-05-24_upgraded_JOB-001.md      ← new, unreviewed
  2026-05-24_missprym_mp-042.md
  accepted/
    2026-05-20_upgraded_JOB-099.md    ← user dragged here = thumbs up
  rejected/
    2026-05-19_missprym_mp-007.md     ← user dragged here = thumbs down
```

The `accepted/` and `rejected/` subfolders are created automatically by `DraftFileWriter` on first run. The user moves files using Explorer or OneDrive (works on mobile too). No CLI or tooling required.

### FeedbackLoader

Scans both subfolders and builds a list of `FeedbackEntry`:

```csharp
record FeedbackEntry(string Title, int Score, string Verdict);
// Verdict: "accepted" or "rejected"
```

For each file found, reads the first line (format: `# {title} — Score: {score}/100`) to extract title and score without a DB lookup. Returns up to the 20 most recent entries (by file modification date) to keep the prompt bounded.

### How feedback flows into Claude

**TitlePreFilter** receives a compact feedback summary appended to its system prompt:

```
Recent feedback (use to calibrate inclusivity):
- ACCEPTED (82): "Senior .NET Developer at Volvo"
- REJECTED (71): "Fullstack Developer, on-site only"
- ACCEPTED (79): "Backend Developer C# — remote"
```

This nudges the filter toward including similar accepted titles and excluding similar rejected ones.

**AssignmentAnalyzer** receives the same feedback list as an additional section in the user message:

```
=== Your feedback on previous matches ===
- ACCEPTED (score 82): "Senior .NET Developer at Volvo"
- REJECTED (score 71): "Fullstack Developer, on-site only" — you rejected this, likely due to location
- ACCEPTED (score 79): "Backend Developer C# — remote"

Use this to calibrate your score for the current assignment.
```

If the feedback list is empty (first run), this section is omitted entirely.

---

## Draft File Writer

`DraftFileWriter` creates one `.md` file per analyzed assignment in `DraftsFolder`:

**Filename:** `YYYY-MM-DD_provider_assignmentId.md`
Example: `2026-05-16_upgraded_JOB-001.md`

**File content:**
```markdown
# Senior .NET Developer at Volvo — Score: 87/100

**URL:** https://...
**Provider:** upgraded
**Analyzed:** 2026-05-16 14:32

## Why this matches
Matches your .NET/Azure background and remote preference. 4-month contract.

---

## Cold Email (Kall e-post)

Hej [namn],

Jag hittade er annons ...

---

## Cover Letter (Personligt brev)

Till [företaget],

Med min bakgrund inom .NET ...
```

If `DraftsFolder` does not exist, create it. Create `accepted/` and `rejected/` subfolders on first run if they don't exist. If a draft file for the same assignment already exists, overwrite it.

---

## Matching Email

`MatchingEmailService` sends one email per run when at least one assignment scores `>= ScoreThreshold`. Uses the existing Mailgun infrastructure.

**Subject:** `{n} strong assignment match(es) found`

**Body (plain text):**
```
3 strong assignment matches found — 2026-05-16

── Match 1 — Score: 87/100 ──────────────────────────
Title:    Senior .NET Developer at Volvo
Provider: Upgraded People
URL:      https://...
Why:      Matches your .NET/Azure background. Remote-friendly, 4-month contract.
Draft:    2026-05-16_upgraded_JOB-001.md

── Match 2 — Score: 74/100 ──────────────────────────
Title:    Fullstack Developer — React + C#
Provider: MissPrym
URL:      https://...
Why:      Strong React + C# fit. On-site in Stockholm only.
Draft:    2026-05-16_missprym_mp-042.md

Drafts saved to: C:\Users\azimuth\OneDrive\AssignmentDrafts\
```

Matches are ordered by score descending. If no assignments score above threshold, no email is sent.

---

## Configuration Validation

At startup (before Phase 2), validate:
- `MatchingOptions.AnthropicApiKey` is non-empty
- `MatchingOptions.DraftsFolder` is non-empty and the path is writable
- `ProfileFolder` exists and contains at least the CV file

Throw descriptive `InvalidOperationException` if any check fails. Skip validation entirely when `Matching.Enabled = false`.

---

## .gitignore

Add to `.gitignore`:
```
profile/
```

---

## What Is Explicitly Out of Scope

- Re-analyzing previously analyzed assignments (once in DB, always skipped)
- Feedback reason capture (why the user accepted/rejected — folder move only, no annotation)
- Web UI or dashboard
- Scheduling the analysis separately from the crawl
- Automatic LinkedIn ZIP extraction (user extracts manually, points config at the folder)
