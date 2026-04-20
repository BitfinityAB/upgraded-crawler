# Code Review Improvements Design

**Date:** 2026-04-20
**Project:** UpgradedCrawler
**Scope:** .NET 10 upgrade, refactoring, unit test coverage, architecture fixes

---

## Context

UpgradedCrawler is a .NET 8 console application that crawls 5 Swedish staffing platforms (Upgraded People, Aliant, TeamPilot, Miss Prym, TechRelations) for new job assignments. It stores results in a local SQLite database and sends email notifications via Mailgun. The codebase is functional but has no test coverage, significant duplication across 5 near-identical service implementations, a hardcoded API key, mixed JSON libraries, and FluentAssertions misused in production code.

---

## Implementation Sequence

Work proceeds in this order to minimize risk — each step leaves the codebase in a working, committable state:

1. .NET 10 upgrade
2. Refactoring
3. Unit tests
4. Architecture / design principles fixes
5. Verification

---

## Step 1: .NET 10 Upgrade

### Target framework
Change `<TargetFramework>` from `net8.0` to `net10.0` in both project files:
- `UpgradedCrawler/UpgradedCrawler.csproj`
- `UpgradedCrawler.Core/UpgradedCrawler.Core.csproj`

### Package versions
Bump all Microsoft-owned packages to `10.0.6` (current stable as of April 2026):
- `Microsoft.EntityFrameworkCore.Sqlite`
- `Microsoft.EntityFrameworkCore.Design`
- `Microsoft.Extensions.Configuration.*`
- `Microsoft.Extensions.DependencyInjection`
- `Microsoft.Extensions.Hosting`

`HtmlAgilityPack` stays at `1.12.4` (netstandard2.0, compatible with net10.0).

### Nullable enable
Enable `<Nullable>enable</Nullable>` in `UpgradedCrawler.csproj` (already enabled in Core). Fix any nullable warnings this surfaces — treat them as bugs, not style issues.

### Acceptance
`dotnet build` passes with zero errors and zero warnings on both projects.

---

## Step 2: Refactoring

### 2a. Abstract base class (Template Method pattern)

Introduce `AssignmentServiceBase` in `UpgradedCrawler.Core/Interfaces/` (or a new `UpgradedCrawler.Core/Services/` folder):

```
AssignmentServiceBase : IAssignmentService
  + GetAssignmentAnnouncementsAsync(AppDbContext) : Task<IList<AssignmentAnnouncement>>
      [sealed — owns the full pipeline]
  # FetchAssignmentsAsync() : Task<IEnumerable<(string id, string url, string title)>>
      [abstract — provider-specific fetch + parse]
  # ProviderId : string
      [abstract — e.g. "upgraded", "aliant"]
```

The base class pipeline (in order):
1. Call `FetchAssignmentsAsync()` to get parsed items
2. For each item: check DB for existing `(AssignmentId, ProviderId)` — skip if found
3. Add new `AssignmentAnnouncement` records to context
4. Call `AssignmentCleanupHelper` with the full set of current IDs from the website
5. Save changes
6. Return only the newly added announcements

Each of the 5 provider classes moves to `UpgradedCrawler/Service/` and implements only `ProviderId` and `FetchAssignmentsAsync()`. All shared pipeline code is deleted from the provider classes.

### 2b. MissPrym API key — move to configuration

Add `MissPrymOptions` record to `UpgradedCrawler.Core`:
```csharp
public record MissPrymOptions
{
    public string ApiKey { get; init; } = string.Empty;
}
```

Register and bind in `Program.cs` alongside `MailgunOptions`. Inject via `IOptions<MissPrymOptions>` into `MissPrymAssignmentService`. Remove the hardcoded `private const string apiKey` field.

Add `"MissPrym": { "ApiKey": "" }` to `appsettings.local.template.json`.

### 2c. Unify JSON serialization

Standardize on `System.Text.Json` throughout. Specifically:
- Remove `Newtonsoft.Json` (`mailgun_csharp`) from `UpgradedCrawler.Core.csproj` — it is only needed in the main project for Mailgun
- Replace `[JsonProperty]` attributes on `AssignmentAnnouncement` and DTOs with `[JsonPropertyName]` from `System.Text.Json.Serialization`
- Confirm `mailgun_csharp` remains in `UpgradedCrawler.csproj` only (Mailgun client requires it)

### 2d. Remove FluentAssertions from production code

In `MailgunService`:
- Replace `content.Should().NotBeNull()` with a null guard + throw
- Replace `content.StatusCode.Should().Be(HttpStatusCode.OK)` with `response.EnsureSuccessStatusCode()`
- Remove `FluentAssertions` NuGet reference from `UpgradedCrawler.csproj`

### 2e. Notification toggle

Add `NotificationOptions` to `appsettings.json`:
```json
"Notification": {
  "Enabled": false
}
```

In `Program.cs`, read `NotificationOptions.Enabled` and conditionally invoke `Notification.cs`. No behavior change when `Enabled` is false (default). `Notification.cs` is retained as-is.

Add `"Notification": { "Enabled": false }` to `appsettings.local.template.json`.

---

## Step 3: Unit Tests

### New project

`UpgradedCrawler.Tests` — class library, `net10.0`, xUnit.

**NuGet dependencies:**
- `xunit`
- `xunit.runner.visualstudio`
- `Microsoft.NET.Test.Sdk`
- `NSubstitute`
- `Microsoft.EntityFrameworkCore.Sqlite`
- `coverlet.collector` (for coverage reports)

Add project reference to both `UpgradedCrawler` and `UpgradedCrawler.Core`.

### Test database strategy

`SqliteTestFixture` (implements `IDisposable`):
- Creates a real SQLite database at a temp file path (`Path.GetTempFileName()`)
- Runs EF Core migrations against it (`context.Database.Migrate()`)
- Exposes a factory method for scoped `AppDbContext` instances
- Deletes the file in `Dispose()`

Each test class that needs the DB takes a `SqliteTestFixture` via constructor injection (xUnit fixture sharing with `IClassFixture<SqliteTestFixture>`). Each test gets a fresh context pointing at the same temp DB, isolated within the test class lifetime.

### Test coverage targets

| Test class | What's tested |
|---|---|
| `AssignmentServiceBaseTests` | Pipeline: new assignments are saved and returned; existing assignments are skipped (deduplication); stale assignments are cleaned up |
| `UpgradedAssignmentServiceParserTests` | Given fixture HTML → correct `(id, url, title)` tuples extracted |
| `AliantAssignmentServiceParserTests` | Given fixture HTML → correct tuples |
| `TeamPilotAssignmentServiceParserTests` | Given fixture HTML → correct tuples |
| `MissPrymAssignmentServiceParserTests` | Given fixture JSON → correct tuples |
| `TechRelationsAssignmentServiceParserTests` | Given fixture JSON → correct tuples |
| `MailgunServiceTests` | 2xx response → no exception; non-2xx → exception thrown |
| `AssignmentCleanupHelperTests` | Stale entries removed; recent entries kept; entries still on website kept regardless of age |

### Fixture data

Raw HTML/JSON saved as embedded resources under `UpgradedCrawler.Tests/Fixtures/`:
- `upgraded-sample.html`
- `aliant-sample.html`
- `teampilot-sample.html`
- `missprym-sample.json`
- `techrelations-sample.json`

Each file contains a minimal but realistic sample of what the live site returns, sufficient to exercise the parser.

### HTTP mocking

Provider `FetchAssignmentsAsync()` methods create their own `HttpClient` from a factory. In tests, supply a mock `IHttpClientFactory` (via NSubstitute) that returns an `HttpClient` wrapping a `FakeHttpMessageHandler` — a simple `DelegatingHandler` that returns the fixture content without network calls.

---

## Step 4: Architecture / Design Principles Fixes

### Null safety in HTML parsers

Fix unsafe attribute access in:
- `AliantAssignmentService`: `row.SelectSingleNode(...).Attributes["onclick"].Value` — add null check on both the node and the attribute before accessing `.Value`
- `TeamPilotAssignmentService`: `SelectSingleNode(".../a").Attributes["href"].Value` — same pattern

Pattern: use null-conditional + null-coalescing to skip malformed rows gracefully, consistent with what `UpgradedAssignmentService` already does (`?? ""`).

### Configurable provider list

Move the hardcoded provider array from `Program.cs` to `appsettings.json`:

```json
"Providers": ["upgraded", "aliant", "teampilot", "missprym", "techrelations"]
```

In `Program.cs`, read the array and resolve each entry via `GetKeyedService<IAssignmentService>(provider)`. If an entry returns `null` (unknown key or typo), skip it silently. This requires no additional validation logic — the keyed service registry acts as the allowlist.

`appsettings.local.template.json` should include the full default list so users know the valid values.

### Startup configuration validation

After binding options in `Program.cs`, validate required fields before the crawl loop starts:
- `MailgunOptions`: `ApiKey` and `Domain` must be non-empty
- `MissPrymOptions`: `ApiKey` must be non-empty

Throw a descriptive `InvalidOperationException` at startup if any are missing. This surfaces misconfiguration immediately rather than at first send attempt.

### Logging completeness

Add `ILogging.Log()` calls in `Program.cs` for:
- Each provider run start (provider name)
- Each provider run completion (count of new assignments found)
- Each provider error (with exception message)

### Nullable warnings (from Step 1)

All warnings surfaced by enabling `<Nullable>enable</Nullable>` in the main project must be resolved. No `#pragma warning disable` suppressions.

---

## Step 5: Verification

1. `dotnet build` — zero errors, zero warnings on both projects and test project
2. `dotnet test` — all tests pass, no skips
3. Manual smoke run: `dotnet run -- -f` from the main project directory — app fetches all 5 providers, logs results, exits cleanly
4. Confirm `appsettings.local.template.json` includes all new sections: `MissPrym`, `Notification`

---

## What Is Explicitly Out of Scope

- Retry/resilience logic (Polly etc.) — the app runs on a schedule; transient failures self-resolve
- Structured logging (Serilog, OpenTelemetry) — overkill for a single-user console tool
- Repository pattern abstraction over EF Core — one DbContext is the right level here
- Removing `Notification.cs` — retained, gated by config flag
- Integration tests for `Program.cs` orchestration — the DI wiring is covered by the manual smoke run
