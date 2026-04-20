# Code Review Improvements Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade to .NET 10, extract a shared base class across all 5 provider services, add xUnit+NSubstitute test coverage backed by a real SQLite temp database, and fix architecture issues (null safety, config validation, hardcoded credentials, dead FluentAssertions usage, notification toggle, configurable providers).

**Architecture:** All 5 `IAssignmentService` implementations inherit a new `AssignmentServiceBase` that owns the fetch→deduplicate→cleanup→save pipeline; providers only implement `ProviderId` and `FetchAssignmentsAsync()`. Tests call providers end-to-end with a `FakeHttpMessageHandler` returning fixture HTML/JSON and a real SQLite database created per test class via `SqliteTestFixture`.

**Tech Stack:** .NET 10, C# 13, EF Core 10, xUnit, NSubstitute, HtmlAgilityPack, System.Text.Json, Newtonsoft.Json (retained in Core for email template serialization), mailgun_csharp.

---

## File Map

### New files
| Path | Purpose |
|---|---|
| `UpgradedCrawler/Service/AssignmentServiceBase.cs` | Abstract base — owns pipeline |
| `UpgradedCrawler.Core/Entities/MissPrymOptions.cs` | Config record for MissPrym API key |
| `UpgradedCrawler.Core/Entities/NotificationOptions.cs` | Config record for notification toggle |
| `UpgradedCrawler/Extensions/MailgunExtensions.cs` | Moved from Core (uses mailgun_csharp) |
| `UpgradedCrawler/Extensions/JsonExtensions.cs` | Moved from Core (uses Newtonsoft.Json JObject) |
| `UpgradedCrawler.Tests/UpgradedCrawler.Tests.csproj` | Test project |
| `UpgradedCrawler.Tests/Infrastructure/SqliteTestFixture.cs` | Real SQLite temp DB per test class |
| `UpgradedCrawler.Tests/Infrastructure/FakeHttpMessageHandler.cs` | URL-aware fake HTTP handler |
| `UpgradedCrawler.Tests/Fixtures/upgraded-nonce.html` | Nonce page fixture for Upgraded provider |
| `UpgradedCrawler.Tests/Fixtures/upgraded-assignments.json` | Assignment JSON fixture for Upgraded provider |
| `UpgradedCrawler.Tests/Fixtures/aliant-assignments.html` | HTML fixture for Aliant provider |
| `UpgradedCrawler.Tests/Fixtures/teampilot-assignments.html` | HTML fixture for TeamPilot provider |
| `UpgradedCrawler.Tests/Fixtures/missprym-assignments.json` | JSON fixture for MissPrym provider |
| `UpgradedCrawler.Tests/Fixtures/techrelations-assignments.json` | JSON fixture for TechRelations provider |
| `UpgradedCrawler.Tests/AssignmentServiceBaseTests.cs` | Pipeline tests (dedup, cleanup, save) |
| `UpgradedCrawler.Tests/AssignmentCleanupHelperTests.cs` | Cleanup logic tests |
| `UpgradedCrawler.Tests/ProviderTests/UpgradedAssignmentServiceTests.cs` | Upgraded parser tests |
| `UpgradedCrawler.Tests/ProviderTests/AliantAssignmentServiceTests.cs` | Aliant parser tests |
| `UpgradedCrawler.Tests/ProviderTests/TeamPilotAssignmentServiceTests.cs` | TeamPilot parser tests |
| `UpgradedCrawler.Tests/ProviderTests/MissPrymAssignmentServiceTests.cs` | MissPrym parser tests |
| `UpgradedCrawler.Tests/ProviderTests/TechRelationsAssignmentServiceTests.cs` | TechRelations parser tests |
| `UpgradedCrawler.Tests/MailgunServiceTests.cs` | Email service tests |

### Modified files
| Path | Change |
|---|---|
| `UpgradedCrawler/UpgradedCrawler.csproj` | net10.0, bump packages, remove FluentAssertions |
| `UpgradedCrawler.Core/UpgradedCrawler.Core.csproj` | net10.0, remove mailgun_csharp, add Newtonsoft.Json |
| `UpgradedCrawler.Core/Data/AppDbContext.cs` | Add options constructor for test support |
| `UpgradedCrawler.Core/Entities/AssignmentAnnouncement.cs` | No change — keeps [JsonProperty] for template |
| `UpgradedCrawler/Service/UpgradedAssignmentService.cs` | Extend base class |
| `UpgradedCrawler/Service/AliantAssignmentService.cs` | Extend base class, fix null deref |
| `UpgradedCrawler/Service/TeamPilotAssignmentService.cs` | Extend base class, fix null deref |
| `UpgradedCrawler/Service/MissPrymAssignmentService.cs` | Extend base class, inject MissPrymOptions |
| `UpgradedCrawler/Service/TechRelationsAssignmentService.cs` | Extend base class |
| `UpgradedCrawler/Service/MailgunService.cs` | Remove FluentAssertions, use new Extensions namespace |
| `UpgradedCrawler.Core/Extensions/MailgunExtensions.cs` | Delete (moved to main project) |
| `UpgradedCrawler.Core/Extensions/JsonExtensions.cs` | Delete (moved to main project) |
| `UpgradedCrawler/Program.cs` | Provider list from config, notification toggle, startup validation, per-provider logging |
| `UpgradedCrawler/appsettings.json` | Add Providers array, Notification section, MissPrym section |
| `UpgradedCrawler/appsettings.local.template.json` | Add MissPrym.ApiKey, Notification.Enabled |

---

## Task 1: Upgrade to .NET 10

**Files:**
- Modify: `UpgradedCrawler/UpgradedCrawler.csproj`
- Modify: `UpgradedCrawler.Core/UpgradedCrawler.Core.csproj`

- [ ] **Step 1: Update UpgradedCrawler.csproj**

Replace the entire file content:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="HtmlAgilityPack" Version="1.12.4" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.6" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="10.0.6" />
    <PackageReference Include="Microsoft.Extensions.Configuration.FileExtensions" Version="10.0.6" />
    <PackageReference Include="mailgun_csharp" Version="0.9.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="10.0.6" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.6" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.6" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\UpgradedCrawler.Core\UpgradedCrawler.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Content Include="appsettings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
    </Content>
  </ItemGroup>

  <ItemGroup>
    <None Update="appsettings.local.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Update UpgradedCrawler.Core.csproj**

Replace the entire file content (removes `mailgun_csharp`, adds `Newtonsoft.Json` directly since `AssignmentAnnouncement` uses `[JsonProperty]` for email template serialization):

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.6">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.6" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Verify build**

```bash
cd /c/Project/upgraded-crawler
dotnet build
```

Expected: Build succeeded with 0 errors. There will be nullable warnings — those will be fixed in Task 11.

- [ ] **Step 4: Commit**

```bash
git add UpgradedCrawler/UpgradedCrawler.csproj UpgradedCrawler.Core/UpgradedCrawler.Core.csproj
git commit -m "chore: upgrade to .NET 10 and bump NuGet packages"
```

---

## Task 2: Add DbContext options constructor

**Files:**
- Modify: `UpgradedCrawler.Core/Data/AppDbContext.cs`

The parameterless constructor hardcodes `%LOCALAPPDATA%/assignments.db`. Adding an `options`-based constructor allows tests to inject a temp-file path without touching production behavior.

- [ ] **Step 1: Update AppDbContext.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using UpgradedCrawler.Core.Entities;

namespace UpgradedCrawler.Core.Data;

public class AppDbContext : DbContext
{
    public DbSet<AssignmentAnnouncement>? Assignments { get; set; }
    public string DbPath { get; private set; }

    public AppDbContext()
    {
        var folder = Environment.SpecialFolder.LocalApplicationData;
        var path = Environment.GetFolderPath(folder);
        DbPath = Path.Join(path, "assignments.db");
    }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        DbPath = string.Empty;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
            options.UseSqlite($"Data Source={DbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AssignmentAnnouncement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.HasIndex(e => new { e.AssignmentId, e.ProviderId })
                  .IsUnique()
                  .HasDatabaseName("IX_Assignments_AssignmentId_ProviderId");
        });
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add UpgradedCrawler.Core/Data/AppDbContext.cs
git commit -m "feat: add options constructor to AppDbContext for test support"
```

---

## Task 3: Add config option records

**Files:**
- Create: `UpgradedCrawler.Core/Entities/MissPrymOptions.cs`
- Create: `UpgradedCrawler.Core/Entities/NotificationOptions.cs`

- [ ] **Step 1: Create MissPrymOptions.cs**

```csharp
namespace UpgradedCrawler.Core.Entities;

public record MissPrymOptions
{
    public string ApiKey { get; init; } = string.Empty;
}
```

- [ ] **Step 2: Create NotificationOptions.cs**

```csharp
namespace UpgradedCrawler.Core.Entities;

public record NotificationOptions
{
    public bool Enabled { get; init; } = false;
}
```

- [ ] **Step 3: Verify build**

```bash
dotnet build
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add UpgradedCrawler.Core/Entities/MissPrymOptions.cs UpgradedCrawler.Core/Entities/NotificationOptions.cs
git commit -m "feat: add MissPrymOptions and NotificationOptions config records"
```

---

## Task 4: Move MailgunExtensions and JsonExtensions to main project

`MailgunExtensions.cs` and `JsonExtensions.cs` live in Core but depend on `mailgun_csharp` types (`IMessageBuilder`) which now only exist in the main project. Move them and update namespaces.

**Files:**
- Create: `UpgradedCrawler/Extensions/MailgunExtensions.cs`
- Create: `UpgradedCrawler/Extensions/JsonExtensions.cs`
- Delete: `UpgradedCrawler.Core/Extensions/MailgunExtensions.cs`
- Delete: `UpgradedCrawler.Core/Extensions/JsonExtensions.cs`
- Modify: `UpgradedCrawler/Service/MailgunService.cs` (update using)

- [ ] **Step 1: Create UpgradedCrawler/Extensions/JsonExtensions.cs**

```csharp
using System.Dynamic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace UpgradedCrawler.Extensions;

public static class JsonExtensions
{
    public static JObject ConvertToCamel(this JObject jsonObject)
    {
        var settings = JsonSerializer.Create(new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        });

        return JObject.FromObject(jsonObject.ToObject<ExpandoObject>()!, settings);
    }
}
```

- [ ] **Step 2: Create UpgradedCrawler/Extensions/MailgunExtensions.cs**

```csharp
using Mailgun.Core.Messages;
using Mailgun.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UpgradedCrawler.Extensions;

public static class MailgunExtensions
{
    public static IMessageBuilder SetTemplate(this IMessageBuilder messageBuilder, string templateName, JObject templateData)
    {
        ThrowIf.IsArgumentNull(() => templateName);
        ThrowIf.IsArgumentNull(() => templateData);

        var settings = new JsonSerializerSettings
        {
            ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
        };
        return messageBuilder.AddCustomParameter("template", templateName)
                             .AddCustomParameter("t:variables", JsonConvert.SerializeObject(templateData.ConvertToCamel(), settings));
    }
}
```

- [ ] **Step 3: Update MailgunService.cs — replace the using directive**

Change:
```csharp
using UpgradedCrawler.Core.Extensions;
```
To:
```csharp
using UpgradedCrawler.Extensions;
```

- [ ] **Step 4: Delete old Core extensions**

```bash
rm /c/Project/upgraded-crawler/UpgradedCrawler.Core/Extensions/MailgunExtensions.cs
rm /c/Project/upgraded-crawler/UpgradedCrawler.Core/Extensions/JsonExtensions.cs
```

- [ ] **Step 5: Verify build**

```bash
dotnet build
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "refactor: move MailgunExtensions and JsonExtensions to main project"
```

---

## Task 5: Fix MailgunService — remove FluentAssertions

**Files:**
- Modify: `UpgradedCrawler/Service/MailgunService.cs`

- [ ] **Step 1: Replace MailgunService.cs**

```csharp
using Mailgun.Messages;
using Mailgun.Service;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Extensions;

namespace UpgradedCrawler.Service;

public class MailgunService(IOptions<MailgunOptions> mailgunOptions) : IEmailService
{
    private readonly MailgunOptions _mailgunOptions = mailgunOptions.Value;

    public async Task SendEmail(string fromAddress, string fromName, string to, string subject, ICollection<AssignmentAnnouncement> assignments)
    {
        var mg = new MessageService(_mailgunOptions.ApiKey, null, "api.eu.mailgun.net/v3");

        var message = new MessageBuilder()
            .AddToRecipient(new Recipient { Email = to })
            .SetSubject(subject)
            .SetFromAddress(new Recipient { Email = fromAddress, DisplayName = fromName })
            .SetTemplate(_mailgunOptions.TemplateName, JObject.FromObject(GetMailgunTemplateData(assignments)))
            .GetMessage();

        var response = await mg.SendMessageAsync(_mailgunOptions.Domain, message);
        if (response is null)
            throw new InvalidOperationException("Mailgun SendMessageAsync returned null.");
        response.EnsureSuccessStatusCode();
    }

    private static MailgunTemplateData GetMailgunTemplateData(ICollection<AssignmentAnnouncement> assignments) =>
        new() { Assignments = assignments, MultipleAssignments = assignments.Count > 1 };
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add UpgradedCrawler/Service/MailgunService.cs
git commit -m "refactor: replace FluentAssertions with EnsureSuccessStatusCode in MailgunService"
```

---

## Task 6: Create AssignmentServiceBase

**Files:**
- Create: `UpgradedCrawler/Service/AssignmentServiceBase.cs`

The base class owns the shared pipeline. Providers only implement `ProviderId` and `FetchAssignmentsAsync()`. The `_httpClientFactory` and `_logging` fields are `protected` so derived classes can use them inside `FetchAssignmentsAsync`.

- [ ] **Step 1: Create AssignmentServiceBase.cs**

```csharp
using UpgradedCrawler.Core.Data;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Helpers;

namespace UpgradedCrawler.Service;

public abstract class AssignmentServiceBase(IHttpClientFactory httpClientFactory, ILogging logging) : IAssignmentService
{
    protected readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    protected readonly ILogging _logging = logging;

    protected abstract string ProviderId { get; }

    protected abstract Task<IEnumerable<(string id, string url, string title)>> FetchAssignmentsAsync();

    public async Task<ICollection<AssignmentAnnouncement>> GetAssignmentAnnouncementsAsync(AppDbContext dbContext)
    {
        var fetched = await FetchAssignmentsAsync();
        var newAssignments = new List<AssignmentAnnouncement>();
        var currentWebsiteIds = new HashSet<string>();

        foreach (var (id, url, title) in fetched)
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            currentWebsiteIds.Add(id);

            if (!dbContext.Assignments!.Any(r => r.AssignmentId == id && r.ProviderId == ProviderId))
                newAssignments.Add(new AssignmentAnnouncement(id, url, ProviderId, title, DateTime.Now));
        }

        AssignmentCleanupHelper.CleanupOldAssignments(dbContext, ProviderId, currentWebsiteIds, _logging);
        dbContext.Assignments!.AddRange(newAssignments);
        await dbContext.SaveChangesAsync();

        return newAssignments;
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add UpgradedCrawler/Service/AssignmentServiceBase.cs
git commit -m "feat: add AssignmentServiceBase with shared pipeline (Template Method)"
```

---

## Task 7: Refactor UpgradedAssignmentService

**Files:**
- Modify: `UpgradedCrawler/Service/UpgradedAssignmentService.cs`

- [ ] **Step 1: Replace UpgradedAssignmentService.cs**

```csharp
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using UpgradedCrawler.Core.Interfaces;

namespace UpgradedCrawler.Service;

public partial class UpgradedAssignmentService(IHttpClientFactory httpClientFactory, ILogging logging)
    : AssignmentServiceBase(httpClientFactory, logging)
{
    private const string NoncePattern = @"var\s+bobz\s*=\s*\{\s*""nonce""\s*:\s*""(?<nonce>\w+)""";
    private const string WebsiteUrl = "https://upgraded.se/lediga-uppdrag/";
    private const string AdminUrl = "https://upgraded.se/wp-admin/admin-ajax.php";

    protected override string ProviderId => "upgraded";

    protected override async Task<IEnumerable<(string id, string url, string title)>> FetchAssignmentsAsync()
    {
        var nonce = await GetNonce();
        if (string.IsNullOrEmpty(nonce))
        {
            _logging.Log("Upgraded: nonce not found, skipping.");
            return [];
        }

        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/javascript, */*; q=0.01");
        httpClient.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");

        var formData = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("action", "do_filter_posts"),
            new KeyValuePair<string, string>("nonce", nonce),
            new KeyValuePair<string, string>("params[ort-term]", "alla-orter"),
            new KeyValuePair<string, string>("params[roll-term]", "alla-roller"),
            new KeyValuePair<string, string>("params[kund-term]", "alla-kunder"),
        ]);

        var response = await httpClient.PostAsync(AdminUrl, formData);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(responseString);
        var htmlContent = jsonDoc.RootElement.GetProperty("content").GetString() ?? string.Empty;

        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(htmlContent);

        var rows = htmlDoc.DocumentNode.SelectNodes("//table/tr[position()>1]");
        if (rows is null || rows.Count == 0)
        {
            _logging.Log("Upgraded: no data rows found.");
            return [];
        }

        var results = new List<(string, string, string)>();
        foreach (var row in rows)
        {
            var url = row.SelectSingleNode("td[1]/div[1]/div/div[1]/a")?.GetAttributeValue("href", "") ?? "";
            var title = row.SelectSingleNode("td[1]/div[2]/h5")?.InnerText.Trim() ?? "";
            var id = row.SelectSingleNode("td[1]/div[1]/div/div[2]/span[1]")?.InnerText.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(id))
            {
                _logging.Log($"Upgraded: failed to extract ID. URL: {url}, Title: {title}");
                continue;
            }
            results.Add((id, url, title));
        }
        return results;
    }

    private async Task<string> GetNonce()
    {
        var httpClient = _httpClientFactory.CreateClient();
        var response = await httpClient.GetAsync(WebsiteUrl);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var match = NonceRegex().Match(content);
        return match.Success && match.Groups["nonce"].Success ? match.Groups["nonce"].Value : string.Empty;
    }

    [GeneratedRegex(NoncePattern)]
    private static partial Regex NonceRegex();
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add UpgradedCrawler/Service/UpgradedAssignmentService.cs
git commit -m "refactor: UpgradedAssignmentService extends AssignmentServiceBase"
```

---

## Task 8: Refactor AliantAssignmentService

**Files:**
- Modify: `UpgradedCrawler/Service/AliantAssignmentService.cs`

Also fixes the unsafe `row.Attributes["onclick"].Value` — if the `onclick` attribute is missing, the old code throws a `NullReferenceException`.

- [ ] **Step 1: Replace AliantAssignmentService.cs**

```csharp
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using UpgradedCrawler.Core.Interfaces;

namespace UpgradedCrawler.Service;

public partial class AliantAssignmentService(IHttpClientFactory httpClientFactory, ILogging logging)
    : AssignmentServiceBase(httpClientFactory, logging)
{
    private const string BaseUrl = "https://aliant.recman.se";
    private const string JobIdPattern = @"job_id=(\d+)";

    protected override string ProviderId => "aliant";

    protected override async Task<IEnumerable<(string id, string url, string title)>> FetchAssignmentsAsync()
    {
        var httpClient = _httpClientFactory.CreateClient();
        var response = await httpClient.GetAsync($"{BaseUrl}/index.php");
        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();

        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(responseString);

        var container = htmlDoc.DocumentNode.SelectSingleNode("//div[contains(@id, 'job-post-listing-box')]");
        if (container is null)
        {
            _logging.Log("Aliant: job listing container not found.");
            return [];
        }

        var results = new List<(string, string, string)>();
        foreach (var row in container.ChildNodes)
        {
            if (row.Name != "div") continue;

            var onclick = row.Attributes["onclick"]?.Value;
            if (onclick is null) continue;

            var match = JobIdRegex().Match(onclick);
            var id = match.Success ? match.Groups[1].Value : "";
            if (string.IsNullOrEmpty(id)) continue;

            var url = $"{BaseUrl}/job.php?job_id={id}";
            var title = row.SelectSingleNode("./div/table/tr/td[2]/span")?.InnerText.Trim() ?? "";
            results.Add((id, url, title));
        }
        return results;
    }

    [GeneratedRegex(JobIdPattern)]
    private static partial Regex JobIdRegex();
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add UpgradedCrawler/Service/AliantAssignmentService.cs
git commit -m "refactor: AliantAssignmentService extends base, fix null onclick deref"
```

---

## Task 9: Refactor TeamPilotAssignmentService

**Files:**
- Modify: `UpgradedCrawler/Service/TeamPilotAssignmentService.cs`

Also fixes the unsafe `SelectSingleNode(...).Attributes["href"].Value` on line 55 of the original.

- [ ] **Step 1: Replace TeamPilotAssignmentService.cs**

```csharp
using HtmlAgilityPack;
using UpgradedCrawler.Core.Interfaces;

namespace UpgradedCrawler.Service;

public class TeamPilotAssignmentService(IHttpClientFactory httpClientFactory, ILogging logging)
    : AssignmentServiceBase(httpClientFactory, logging)
{
    private const string BaseUrl = "https://app.teampilot.io";

    protected override string ProviderId => "teampilot";

    protected override async Task<IEnumerable<(string id, string url, string title)>> FetchAssignmentsAsync()
    {
        var httpClient = _httpClientFactory.CreateClient();
        var response = await httpClient.GetAsync($"{BaseUrl}/jobs");
        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();

        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(responseString);

        var activeHeader = htmlDoc.DocumentNode.SelectSingleNode("//h3[contains(text(), 'Active Positions')]");
        if (activeHeader is null)
        {
            _logging.Log("TeamPilot: Active Positions header not found.");
            return [];
        }

        var rows = activeHeader.SelectSingleNode(
            "following-sibling::div[@class='row' and following-sibling::h3[contains(text(), 'Historical Positions')]][1]");
        if (rows is null)
        {
            _logging.Log("TeamPilot: no rows found under Active Positions.");
            return [];
        }

        var results = new List<(string, string, string)>();
        foreach (var row in rows.ChildNodes)
        {
            if (row.Name != "div") continue;

            var href = row.SelectSingleNode("./div/div[2]/div[contains(@class, 'd-grid')]/a")
                          ?.Attributes["href"]?.Value;
            if (href is null) continue;

            var id = href.Split("/job/").ElementAtOrDefault(1) ?? "";
            if (string.IsNullOrEmpty(id)) continue;

            var url = BaseUrl + href;
            var title = row.SelectSingleNode("./div/div[2]/h5")?.InnerText.Trim() ?? "";
            results.Add((id, url, title));
        }
        return results;
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add UpgradedCrawler/Service/TeamPilotAssignmentService.cs
git commit -m "refactor: TeamPilotAssignmentService extends base, fix null href deref"
```

---

## Task 10: Refactor MissPrymAssignmentService

**Files:**
- Modify: `UpgradedCrawler/Service/MissPrymAssignmentService.cs`

Moves the hardcoded API key to `IOptions<MissPrymOptions>`.

- [ ] **Step 1: Replace MissPrymAssignmentService.cs**

```csharp
using System.Text.Json;
using Microsoft.Extensions.Options;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Core.Interfaces;

namespace UpgradedCrawler.Service;

public class MissPrymAssignmentService(
    IHttpClientFactory httpClientFactory,
    ILogging logging,
    IOptions<MissPrymOptions> options)
    : AssignmentServiceBase(httpClientFactory, logging)
{
    private const string BaseUrl = "https://hint.missprym.com";
    private const string ApiUrl = "https://mint-webapi.azurewebsites.net/Assignments/PublicEnriched";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly string _apiKey = options.Value.ApiKey;

    protected override string ProviderId => "missprym";

    protected override async Task<IEnumerable<(string id, string url, string title)>> FetchAssignmentsAsync()
    {
        var httpClient = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, ApiUrl);
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("Origin", BaseUrl);
        request.Headers.Add("Referer", $"{BaseUrl}/");

        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();

        var assignments = JsonSerializer.Deserialize<List<MissPrymAssignment>>(responseString, JsonOptions);
        if (assignments is null || assignments.Count == 0)
        {
            _logging.Log("MissPrym: no assignments in API response.");
            return [];
        }

        var results = new List<(string, string, string)>();
        foreach (var assignment in assignments)
        {
            if (string.IsNullOrEmpty(assignment.Id)) continue;
            var url = $"{BaseUrl}/job-posting/{assignment.Id}";
            results.Add((assignment.Id, url, assignment.Title ?? ""));
        }
        return results;
    }
}

internal class MissPrymAssignment
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add UpgradedCrawler/Service/MissPrymAssignmentService.cs
git commit -m "refactor: MissPrymAssignmentService extends base, move API key to config"
```

---

## Task 11: Refactor TechRelationsAssignmentService

**Files:**
- Modify: `UpgradedCrawler/Service/TechRelationsAssignmentService.cs`

- [ ] **Step 1: Replace TechRelationsAssignmentService.cs**

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using UpgradedCrawler.Core.Interfaces;

namespace UpgradedCrawler.Service;

public class TechRelationsAssignmentService(IHttpClientFactory httpClientFactory, ILogging logging)
    : AssignmentServiceBase(httpClientFactory, logging)
{
    private const string ApiUrl = "https://www.techrelations.se/api/getAssignments?perPage=60";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    protected override string ProviderId => "techrelations";

    protected override async Task<IEnumerable<(string id, string url, string title)>> FetchAssignmentsAsync()
    {
        var httpClient = _httpClientFactory.CreateClient();
        var response = await httpClient.GetAsync(ApiUrl);
        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();

        var assignments = JsonSerializer.Deserialize<List<TechRelationsAssignment>>(responseString, JsonOptions);
        if (assignments is null || assignments.Count == 0)
        {
            _logging.Log("TechRelations: no assignments in API response.");
            return [];
        }

        var results = new List<(string, string, string)>();
        foreach (var assignment in assignments)
        {
            if (assignment.Acf?.Assigned != false) continue;

            var id = assignment.Id.ToString();
            var url = assignment.Link?.Replace(
                "https://admin.techrelations.se/assignments",
                "https://www.techrelations.se/konsultuppdrag") ?? "";
            var title = assignment.Title?.Rendered ?? "";
            results.Add((id, url, title));
        }
        return results;
    }
}

internal class TechRelationsAssignment
{
    public int Id { get; set; }

    [JsonPropertyName("link")]
    public string? Link { get; set; }

    [JsonPropertyName("title")]
    public TechRelationsTitle? Title { get; set; }

    [JsonPropertyName("acf")]
    public TechRelationsAcf? Acf { get; set; }
}

internal class TechRelationsTitle
{
    [JsonPropertyName("rendered")]
    public string? Rendered { get; set; }
}

internal class TechRelationsAcf
{
    [JsonPropertyName("assigned")]
    public bool Assigned { get; set; }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add UpgradedCrawler/Service/TechRelationsAssignmentService.cs
git commit -m "refactor: TechRelationsAssignmentService extends AssignmentServiceBase"
```

---

## Task 12: Update Program.cs

**Files:**
- Modify: `UpgradedCrawler/Program.cs`

Changes: register `MissPrymOptions` and `NotificationOptions`, read providers list from config (silently skip unknowns), add startup validation, add per-provider logging, conditionally invoke notification.

- [ ] **Step 1: Replace Program.cs**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using UpgradedCrawler.Core.Data;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Helpers;
using UpgradedCrawler.Service;

var forceRun = args.Contains("-f") || args.Contains("--force");
var logToEventLog = args.Contains("-e") || args.Contains("--eventlog");
var logger = new Logging(logToEventLog);

try
{
    if (!forceRun && !IsWorkingHour())
    {
        logger.Log("Not working hours. Exiting.");
        return;
    }

    var host = Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((_, config) =>
        {
            var localSettings = Path.Combine(AppContext.BaseDirectory, "appsettings.local.json");
            if (File.Exists(localSettings))
                config.AddJsonFile(localSettings, optional: true, reloadOnChange: true);
        })
        .ConfigureServices((context, services) =>
        {
            services.AddKeyedScoped<IAssignmentService, UpgradedAssignmentService>("upgraded");
            services.AddKeyedScoped<IAssignmentService, AliantAssignmentService>("aliant");
            services.AddKeyedScoped<IAssignmentService, TeamPilotAssignmentService>("teampilot");
            services.AddKeyedScoped<IAssignmentService, MissPrymAssignmentService>("missprym");
            services.AddKeyedScoped<IAssignmentService, TechRelationsAssignmentService>("techrelations");
            services.AddScoped<ILogging>(_ => new Logging(logToEventLog));
            services.AddScoped<IEmailService, MailgunService>();
            services.AddDbContext<AppDbContext>();

            services.AddHttpClient<IAssignmentService, UpgradedAssignmentService>();
            services.AddHttpClient<IAssignmentService, AliantAssignmentService>();
            services.AddHttpClient<IAssignmentService, TeamPilotAssignmentService>();
            services.AddHttpClient<IAssignmentService, MissPrymAssignmentService>();
            services.AddHttpClient<IAssignmentService, TechRelationsAssignmentService>();

            services.Configure<MailgunOptions>(context.Configuration.GetSection("mailgun"));
            services.Configure<MissPrymOptions>(context.Configuration.GetSection("MissPrym"));
            services.Configure<NotificationOptions>(context.Configuration.GetSection("Notification"));
        })
        .Build();

    // Validate required configuration at startup
    var mailgunOpts = host.Services.GetRequiredService<IOptions<MailgunOptions>>().Value;
    if (string.IsNullOrWhiteSpace(mailgunOpts.ApiKey))
        throw new InvalidOperationException("Mailgun ApiKey is not configured in appsettings.");
    if (string.IsNullOrWhiteSpace(mailgunOpts.Domain))
        throw new InvalidOperationException("Mailgun Domain is not configured in appsettings.");

    var missPrymOpts = host.Services.GetRequiredService<IOptions<MissPrymOptions>>().Value;
    if (string.IsNullOrWhiteSpace(missPrymOpts.ApiKey))
        throw new InvalidOperationException("MissPrym ApiKey is not configured in appsettings.");

    var db = host.Services.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();

    var providers = host.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()
        .GetSection("Providers").Get<string[]>() ?? [];

    var emailService = host.Services.GetRequiredService<IEmailService>();
    var newAssignments = new List<AssignmentAnnouncement>();

    foreach (var provider in providers)
    {
        var service = host.Services.GetKeyedService<IAssignmentService>(provider);
        if (service is null)
        {
            logger.Log($"Warning: unknown provider '{provider}' in config. Skipping.");
            continue;
        }

        logger.Log($"Fetching assignments from '{provider}'...");
        try
        {
            var found = await service.GetAssignmentAnnouncementsAsync(db);
            logger.Log($"'{provider}': {found.Count} new assignment(s).");
            newAssignments.AddRange(found);
        }
        catch (Exception ex)
        {
            logger.Log($"'{provider}' error: {ex.Message}");
        }
    }

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

    var notificationOpts = host.Services.GetRequiredService<IOptions<NotificationOptions>>().Value;
    if (notificationOpts.Enabled && OperatingSystem.IsMacOS())
        Notification.ShowMacNotification("New Assignments", $"{newAssignments.Count} new assignment{suffix} found.");
}
catch (Exception ex)
{
    logger.Log($"Error: {ex.Message}");
}

static bool IsWorkingHour()
{
    var now = DateTime.Now;
    return now.DayOfWeek != DayOfWeek.Saturday
        && now.DayOfWeek != DayOfWeek.Sunday
        && now.Hour >= 8
        && now.Hour < 17;
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build
```

Expected: Build succeeded. Nullable warnings may appear — address each one (they should be minimal at this point).

- [ ] **Step 3: Commit**

```bash
git add UpgradedCrawler/Program.cs
git commit -m "feat: provider list from config, startup validation, notification toggle, per-provider logging"
```

---

## Task 13: Update appsettings.json and template

**Files:**
- Modify: `UpgradedCrawler/appsettings.json`
- Modify: `UpgradedCrawler/appsettings.local.template.json` (create if it doesn't exist)

- [ ] **Step 1: Replace appsettings.json**

```json
{
  "mailgun": {
    "fromAddress": "",
    "fromName": "",
    "to": "",
    "apiKey": "",
    "domain": "",
    "templateName": "upgraded notification email"
  },
  "MissPrym": {
    "ApiKey": ""
  },
  "Notification": {
    "Enabled": false
  },
  "Providers": ["upgraded", "aliant", "teampilot", "missprym", "techrelations"]
}
```

- [ ] **Step 2: Update appsettings.local.template.json**

```json
{
  "mailgun": {
    "fromAddress": "sender@example.com",
    "fromName": "Assignment Crawler",
    "to": "recipient@example.com",
    "apiKey": "<your-mailgun-api-key>",
    "domain": "<your-mailgun-domain>",
    "templateName": "upgraded notification email"
  },
  "MissPrym": {
    "ApiKey": "<your-missprym-api-key>"
  },
  "Notification": {
    "Enabled": false
  },
  "Providers": ["upgraded", "aliant", "teampilot", "missprym", "techrelations"]
}
```

- [ ] **Step 3: Verify build and run**

```bash
dotnet build
```

Expected: Build succeeded, 0 errors, 0 warnings.

- [ ] **Step 4: Commit**

```bash
git add UpgradedCrawler/appsettings.json UpgradedCrawler/appsettings.local.template.json
git commit -m "feat: add MissPrym, Notification, and Providers sections to appsettings"
```

---

## Task 14: Create test project

**Files:**
- Create: `UpgradedCrawler.Tests/UpgradedCrawler.Tests.csproj`

- [ ] **Step 1: Create the test project file**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="NSubstitute" Version="5.3.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.6" />
    <PackageReference Include="coverlet.collector" Version="6.0.4">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\UpgradedCrawler\UpgradedCrawler.csproj" />
    <ProjectReference Include="..\UpgradedCrawler.Core\UpgradedCrawler.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <EmbeddedResource Include="Fixtures\**\*" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Add test project to solution**

```bash
cd /c/Project/upgraded-crawler
dotnet sln add UpgradedCrawler.Tests/UpgradedCrawler.Tests.csproj
```

- [ ] **Step 3: Verify test project builds**

```bash
dotnet build UpgradedCrawler.Tests/UpgradedCrawler.Tests.csproj
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add UpgradedCrawler.Tests/UpgradedCrawler.Tests.csproj
git add *.sln
git commit -m "chore: add UpgradedCrawler.Tests project"
```

---

## Task 15: Create test infrastructure

**Files:**
- Create: `UpgradedCrawler.Tests/Infrastructure/SqliteTestFixture.cs`
- Create: `UpgradedCrawler.Tests/Infrastructure/FakeHttpMessageHandler.cs`

- [ ] **Step 1: Create SqliteTestFixture.cs**

Each test class that uses the DB declares `IClassFixture<SqliteTestFixture>`. The fixture creates a fresh SQLite file, runs EF migrations once per test class, and deletes the file on dispose.

```csharp
using Microsoft.EntityFrameworkCore;
using UpgradedCrawler.Core.Data;

namespace UpgradedCrawler.Tests.Infrastructure;

public sealed class SqliteTestFixture : IDisposable
{
    private readonly string _dbPath;

    public SqliteTestFixture()
    {
        _dbPath = Path.GetTempFileName();
        using var context = CreateContext();
        context.Database.Migrate();
    }

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        return new AppDbContext(options);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
```

- [ ] **Step 2: Create FakeHttpMessageHandler.cs**

URL-aware handler: returns a configured response for each registered URL, and a 404 for anything else. Allows `UpgradedAssignmentService` (which makes 2 HTTP calls to different URLs) to be tested without real network access.

```csharp
using System.Net;

namespace UpgradedCrawler.Tests.Infrastructure;

public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, HttpResponseMessage> _responses;

    public FakeHttpMessageHandler(Dictionary<string, HttpResponseMessage> responses)
    {
        _responses = responses;
    }

    public FakeHttpMessageHandler(string content, HttpStatusCode status = HttpStatusCode.OK)
    {
        _responses = new Dictionary<string, HttpResponseMessage>
        {
            ["*"] = new HttpResponseMessage(status) { Content = new StringContent(content) }
        };
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri?.ToString() ?? "";
        if (_responses.TryGetValue(url, out var response))
            return Task.FromResult(response);
        if (_responses.TryGetValue("*", out var fallback))
            return Task.FromResult(fallback);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
```

- [ ] **Step 3: Verify build**

```bash
dotnet build UpgradedCrawler.Tests/UpgradedCrawler.Tests.csproj
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add UpgradedCrawler.Tests/Infrastructure/
git commit -m "test: add SqliteTestFixture and FakeHttpMessageHandler infrastructure"
```

---

## Task 16: Create fixture data files

**Files:**
- Create: `UpgradedCrawler.Tests/Fixtures/upgraded-nonce.html`
- Create: `UpgradedCrawler.Tests/Fixtures/upgraded-assignments.json`
- Create: `UpgradedCrawler.Tests/Fixtures/aliant-assignments.html`
- Create: `UpgradedCrawler.Tests/Fixtures/teampilot-assignments.html`
- Create: `UpgradedCrawler.Tests/Fixtures/missprym-assignments.json`
- Create: `UpgradedCrawler.Tests/Fixtures/techrelations-assignments.json`

Each file is a minimal real-structure sample that exercises the parser's XPath/JSON logic.

- [ ] **Step 1: Create upgraded-nonce.html**

Must contain the nonce pattern: `var\s+bobz\s*=\s*\{\s*"nonce"\s*:\s*"(?<nonce>\w+)"`

```html
<!DOCTYPE html>
<html>
<head>
<script>var bobz = {"nonce": "testnonce123"}</script>
</head>
<body></body>
</html>
```

- [ ] **Step 2: Create upgraded-assignments.json**

The Upgraded API returns `{"content": "<html table>"}`. The table rows must match XPaths:
- URL: `td[1]/div[1]/div/div[1]/a` → href
- ID: `td[1]/div[1]/div/div[2]/span[1]` → InnerText
- Title: `td[1]/div[2]/h5` → InnerText

```json
{"content": "<table><tr><th>Header</th></tr><tr><td><div><div><div><a href=\"https://upgraded.se/job/JOB-001\">link</a></div><div><span>JOB-001</span></div></div></div><div><h5>Upgraded Test Assignment</h5></div></td></tr></table>"}
```

- [ ] **Step 3: Create aliant-assignments.html**

Container: `//div[contains(@id, 'job-post-listing-box')]`
Child divs: `onclick` with `job_id=(\d+)`, title at `./div/table/tr/td[2]/span`

```html
<!DOCTYPE html>
<html>
<body>
<div id="job-post-listing-box">
  <div onclick="navigate('job_id=456')">
    <div>
      <table>
        <tr>
          <td></td>
          <td><span>Aliant Test Assignment</span></td>
        </tr>
      </table>
    </div>
  </div>
</div>
</body>
</html>
```

- [ ] **Step 4: Create teampilot-assignments.html**

H3 "Active Positions", following-sibling div[@class='row'], child divs with href at `./div/div[2]/div[contains(@class,'d-grid')]/a` and title at `./div/div[2]/h5`

```html
<!DOCTYPE html>
<html>
<body>
<h3>Active Positions</h3>
<div class="row">
  <div>
    <div>
      <div></div>
      <div>
        <div class="d-grid">
          <a href="/job/789">View</a>
        </div>
        <h5>TeamPilot Test Assignment</h5>
      </div>
    </div>
  </div>
</div>
<h3>Historical Positions</h3>
</body>
</html>
```

- [ ] **Step 5: Create missprym-assignments.json**

```json
[
  {"Id": "mp-001", "Title": "MissPrym Test Assignment"}
]
```

- [ ] **Step 6: Create techrelations-assignments.json**

```json
[
  {
    "id": 999,
    "link": "https://admin.techrelations.se/assignments/999",
    "title": {"rendered": "TechRelations Test Assignment"},
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

(The second entry has `assigned: true` — it must be excluded by the parser.)

- [ ] **Step 7: Verify build**

```bash
dotnet build UpgradedCrawler.Tests/UpgradedCrawler.Tests.csproj
```

Expected: Build succeeded.

- [ ] **Step 8: Commit**

```bash
git add UpgradedCrawler.Tests/Fixtures/
git commit -m "test: add HTML and JSON fixture files for all 5 providers"
```

---

## Task 17: Write AssignmentServiceBase and CleanupHelper tests

**Files:**
- Create: `UpgradedCrawler.Tests/AssignmentServiceBaseTests.cs`
- Create: `UpgradedCrawler.Tests/AssignmentCleanupHelperTests.cs`

To test `AssignmentServiceBase` in isolation, create a `FakeAssignmentService` inside the test that overrides `FetchAssignmentsAsync` with controlled data.

- [ ] **Step 1: Write failing tests for AssignmentServiceBase**

Create `UpgradedCrawler.Tests/AssignmentServiceBaseTests.cs`:

```csharp
using NSubstitute;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Service;
using UpgradedCrawler.Tests.Infrastructure;

namespace UpgradedCrawler.Tests;

public class AssignmentServiceBaseTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>
{
    private readonly SqliteTestFixture _fixture = fixture;

    private static FakeService CreateService(
        IEnumerable<(string id, string url, string title)> items)
    {
        var logging = Substitute.For<ILogging>();
        var factory = Substitute.For<IHttpClientFactory>();
        return new FakeService(factory, logging, items);
    }

    [Fact]
    public async Task NewAssignments_AreReturnedAndPersisted()
    {
        using var db = _fixture.CreateContext();
        var service = CreateService([("id-1", "https://example.com/1", "Title One")]);

        var result = await service.GetAssignmentAnnouncementsAsync(db);

        Assert.Single(result);
        Assert.Equal("id-1", result.First().AssignmentId);
        Assert.Equal("https://example.com/1", result.First().Url);
        Assert.Equal("Title One", result.First().Title);
        Assert.Single(db.Assignments!.Where(a => a.ProviderId == "fake"));
    }

    [Fact]
    public async Task ExistingAssignment_IsNotReturnedAgain()
    {
        using var db = _fixture.CreateContext();
        var service = CreateService([("id-dup", "https://example.com/dup", "Dup")]);

        await service.GetAssignmentAnnouncementsAsync(db);
        var secondRun = await service.GetAssignmentAnnouncementsAsync(db);

        Assert.Empty(secondRun);
        Assert.Single(db.Assignments!.Where(a => a.AssignmentId == "id-dup" && a.ProviderId == "fake"));
    }

    [Fact]
    public async Task OldAssignmentNotOnWebsite_IsRemovedAfter30Days()
    {
        using var db = _fixture.CreateContext();

        // Seed an old assignment that is NOT in the new fetch results
        var old = new UpgradedCrawler.Core.Entities.AssignmentAnnouncement(
            "old-id", "https://example.com/old", "fake", "Old Title", DateTime.Now.AddDays(-31));
        db.Assignments!.Add(old);
        await db.SaveChangesAsync();

        // New fetch returns a different assignment — old one should be cleaned up
        var service = CreateService([("new-id", "https://example.com/new", "New")]);
        await service.GetAssignmentAnnouncementsAsync(db);

        Assert.DoesNotContain(db.Assignments!, a => a.AssignmentId == "old-id");
    }

    [Fact]
    public async Task RecentAssignmentNotOnWebsite_IsKeptWithin30Days()
    {
        using var db = _fixture.CreateContext();

        var recent = new UpgradedCrawler.Core.Entities.AssignmentAnnouncement(
            "recent-id", "https://example.com/recent", "fake", "Recent Title", DateTime.Now.AddDays(-5));
        db.Assignments!.Add(recent);
        await db.SaveChangesAsync();

        var service = CreateService([("other-id", "https://example.com/other", "Other")]);
        await service.GetAssignmentAnnouncementsAsync(db);

        Assert.Contains(db.Assignments!, a => a.AssignmentId == "recent-id");
    }

    private sealed class FakeService(
        IHttpClientFactory factory,
        ILogging logging,
        IEnumerable<(string id, string url, string title)> items)
        : AssignmentServiceBase(factory, logging)
    {
        protected override string ProviderId => "fake";
        protected override Task<IEnumerable<(string id, string url, string title)>> FetchAssignmentsAsync()
            => Task.FromResult(items);
    }
}
```

- [ ] **Step 2: Run — expect failure (FakeService can't access AssignmentServiceBase internals yet)**

```bash
dotnet test UpgradedCrawler.Tests/UpgradedCrawler.Tests.csproj --filter "AssignmentServiceBaseTests"
```

Expected: Build succeeds. If tests fail, verify `FakeService` can access `AssignmentServiceBase` — it's in the same assembly, so no `InternalsVisibleTo` needed.

- [ ] **Step 3: Write AssignmentCleanupHelperTests.cs**

```csharp
using NSubstitute;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Helpers;
using UpgradedCrawler.Tests.Infrastructure;

namespace UpgradedCrawler.Tests;

public class AssignmentCleanupHelperTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>
{
    private readonly SqliteTestFixture _fixture = fixture;

    [Fact]
    public async Task StaleAssignment_NotOnWebsite_IsRemoved()
    {
        using var db = _fixture.CreateContext();
        var logging = Substitute.For<ILogging>();

        db.Assignments!.Add(new("stale-1", "https://example.com", "p1", "Stale", DateTime.Now.AddDays(-31)));
        await db.SaveChangesAsync();

        AssignmentCleanupHelper.CleanupOldAssignments(db, "p1", [], logging);
        await db.SaveChangesAsync();

        Assert.Empty(db.Assignments!.Where(a => a.ProviderId == "p1"));
    }

    [Fact]
    public async Task RecentAssignment_NotOnWebsite_IsKept()
    {
        using var db = _fixture.CreateContext();
        var logging = Substitute.For<ILogging>();

        db.Assignments!.Add(new("recent-1", "https://example.com", "p2", "Recent", DateTime.Now.AddDays(-5)));
        await db.SaveChangesAsync();

        AssignmentCleanupHelper.CleanupOldAssignments(db, "p2", [], logging);
        await db.SaveChangesAsync();

        Assert.Single(db.Assignments!.Where(a => a.ProviderId == "p2"));
    }

    [Fact]
    public async Task OldAssignment_StillOnWebsite_IsKept()
    {
        using var db = _fixture.CreateContext();
        var logging = Substitute.For<ILogging>();

        db.Assignments!.Add(new("active-1", "https://example.com", "p3", "Active", DateTime.Now.AddDays(-60)));
        await db.SaveChangesAsync();

        AssignmentCleanupHelper.CleanupOldAssignments(db, "p3", ["active-1"], logging);
        await db.SaveChangesAsync();

        Assert.Single(db.Assignments!.Where(a => a.ProviderId == "p3"));
    }
}
```

- [ ] **Step 4: Run all tests so far**

```bash
dotnet test UpgradedCrawler.Tests/UpgradedCrawler.Tests.csproj --filter "AssignmentServiceBaseTests|AssignmentCleanupHelperTests"
```

Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add UpgradedCrawler.Tests/AssignmentServiceBaseTests.cs UpgradedCrawler.Tests/AssignmentCleanupHelperTests.cs
git commit -m "test: add AssignmentServiceBase pipeline and CleanupHelper tests"
```

---

## Task 18: Write provider parser tests

**Files:**
- Create: `UpgradedCrawler.Tests/ProviderTests/UpgradedAssignmentServiceTests.cs`
- Create: `UpgradedCrawler.Tests/ProviderTests/AliantAssignmentServiceTests.cs`
- Create: `UpgradedCrawler.Tests/ProviderTests/TeamPilotAssignmentServiceTests.cs`
- Create: `UpgradedCrawler.Tests/ProviderTests/MissPrymAssignmentServiceTests.cs`
- Create: `UpgradedCrawler.Tests/ProviderTests/TechRelationsAssignmentServiceTests.cs`

Each test loads its fixture file via `Assembly.GetManifestResourceStream`, supplies it to a `FakeHttpMessageHandler`, calls the service, and asserts the parsed output.

Helper to load fixture content — add at the top of each test file or in a shared helper:

```csharp
private static string LoadFixture(string filename)
{
    var assembly = typeof(SqliteTestFixture).Assembly;
    var resourceName = assembly.GetManifestResourceNames()
        .Single(n => n.EndsWith(filename));
    using var stream = assembly.GetManifestResourceStream(resourceName)!;
    using var reader = new StreamReader(stream);
    return reader.ReadToEnd();
}
```

- [ ] **Step 1: Write UpgradedAssignmentServiceTests.cs**

The Upgraded provider makes TWO HTTP calls: GET to `/lediga-uppdrag/` (nonce page) and POST to `/wp-admin/admin-ajax.php` (assignments JSON). Use URL-keyed responses.

```csharp
using System.Net;
using Microsoft.Extensions.Options;
using NSubstitute;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Service;
using UpgradedCrawler.Tests.Infrastructure;

namespace UpgradedCrawler.Tests.ProviderTests;

public class UpgradedAssignmentServiceTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>
{
    private readonly SqliteTestFixture _fixture = fixture;

    private static string LoadFixture(string filename)
    {
        var assembly = typeof(SqliteTestFixture).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(filename));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public async Task ParsesAssignmentFromFixture()
    {
        using var db = _fixture.CreateContext();
        var logging = Substitute.For<ILogging>();

        var handler = new FakeHttpMessageHandler(new Dictionary<string, HttpResponseMessage>
        {
            ["https://upgraded.se/lediga-uppdrag/"] =
                new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(LoadFixture("upgraded-nonce.html")) },
            ["https://upgraded.se/wp-admin/admin-ajax.php"] =
                new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(LoadFixture("upgraded-assignments.json")) },
        });

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));

        var service = new UpgradedAssignmentService(factory, logging);
        var result = await service.GetAssignmentAnnouncementsAsync(db);

        Assert.Single(result);
        Assert.Equal("JOB-001", result.First().AssignmentId);
        Assert.Equal("https://upgraded.se/job/JOB-001", result.First().Url);
        Assert.Equal("Upgraded Test Assignment", result.First().Title);
    }
}
```

- [ ] **Step 2: Write AliantAssignmentServiceTests.cs**

```csharp
using System.Net;
using NSubstitute;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Service;
using UpgradedCrawler.Tests.Infrastructure;

namespace UpgradedCrawler.Tests.ProviderTests;

public class AliantAssignmentServiceTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>
{
    private readonly SqliteTestFixture _fixture = fixture;

    private static string LoadFixture(string filename)
    {
        var assembly = typeof(SqliteTestFixture).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(filename));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public async Task ParsesAssignmentFromFixture()
    {
        using var db = _fixture.CreateContext();
        var logging = Substitute.For<ILogging>();
        var handler = new FakeHttpMessageHandler(LoadFixture("aliant-assignments.html"));
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));

        var service = new AliantAssignmentService(factory, logging);
        var result = await service.GetAssignmentAnnouncementsAsync(db);

        Assert.Single(result);
        Assert.Equal("456", result.First().AssignmentId);
        Assert.Equal("https://aliant.recman.se/job.php?job_id=456", result.First().Url);
        Assert.Equal("Aliant Test Assignment", result.First().Title);
    }
}
```

- [ ] **Step 3: Write TeamPilotAssignmentServiceTests.cs**

```csharp
using System.Net;
using NSubstitute;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Service;
using UpgradedCrawler.Tests.Infrastructure;

namespace UpgradedCrawler.Tests.ProviderTests;

public class TeamPilotAssignmentServiceTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>
{
    private readonly SqliteTestFixture _fixture = fixture;

    private static string LoadFixture(string filename)
    {
        var assembly = typeof(SqliteTestFixture).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(filename));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public async Task ParsesAssignmentFromFixture()
    {
        using var db = _fixture.CreateContext();
        var logging = Substitute.For<ILogging>();
        var handler = new FakeHttpMessageHandler(LoadFixture("teampilot-assignments.html"));
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));

        var service = new TeamPilotAssignmentService(factory, logging);
        var result = await service.GetAssignmentAnnouncementsAsync(db);

        Assert.Single(result);
        Assert.Equal("789", result.First().AssignmentId);
        Assert.Equal("https://app.teampilot.io/job/789", result.First().Url);
        Assert.Equal("TeamPilot Test Assignment", result.First().Title);
    }
}
```

- [ ] **Step 4: Write MissPrymAssignmentServiceTests.cs**

```csharp
using System.Net;
using Microsoft.Extensions.Options;
using NSubstitute;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Service;
using UpgradedCrawler.Tests.Infrastructure;

namespace UpgradedCrawler.Tests.ProviderTests;

public class MissPrymAssignmentServiceTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>
{
    private readonly SqliteTestFixture _fixture = fixture;

    private static string LoadFixture(string filename)
    {
        var assembly = typeof(SqliteTestFixture).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(filename));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public async Task ParsesAssignmentFromFixture()
    {
        using var db = _fixture.CreateContext();
        var logging = Substitute.For<ILogging>();
        var options = Options.Create(new MissPrymOptions { ApiKey = "test-key" });
        var handler = new FakeHttpMessageHandler(LoadFixture("missprym-assignments.json"));
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));

        var service = new MissPrymAssignmentService(factory, logging, options);
        var result = await service.GetAssignmentAnnouncementsAsync(db);

        Assert.Single(result);
        Assert.Equal("mp-001", result.First().AssignmentId);
        Assert.Equal("https://hint.missprym.com/job-posting/mp-001", result.First().Url);
        Assert.Equal("MissPrym Test Assignment", result.First().Title);
    }
}
```

- [ ] **Step 5: Write TechRelationsAssignmentServiceTests.cs**

Also verifies that `assigned: true` entries are excluded.

```csharp
using System.Net;
using NSubstitute;
using UpgradedCrawler.Core.Interfaces;
using UpgradedCrawler.Service;
using UpgradedCrawler.Tests.Infrastructure;

namespace UpgradedCrawler.Tests.ProviderTests;

public class TechRelationsAssignmentServiceTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>
{
    private readonly SqliteTestFixture _fixture = fixture;

    private static string LoadFixture(string filename)
    {
        var assembly = typeof(SqliteTestFixture).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(filename));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public async Task ParsesOnlyUnassignedAssignmentsFromFixture()
    {
        using var db = _fixture.CreateContext();
        var logging = Substitute.For<ILogging>();
        var handler = new FakeHttpMessageHandler(LoadFixture("techrelations-assignments.json"));
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));

        var service = new TechRelationsAssignmentService(factory, logging);
        var result = await service.GetAssignmentAnnouncementsAsync(db);

        Assert.Single(result);
        Assert.Equal("999", result.First().AssignmentId);
        Assert.Equal("https://www.techrelations.se/konsultuppdrag/999", result.First().Url);
        Assert.Equal("TechRelations Test Assignment", result.First().Title);
    }
}
```

- [ ] **Step 6: Run all provider tests**

```bash
dotnet test UpgradedCrawler.Tests/UpgradedCrawler.Tests.csproj --filter "ProviderTests"
```

Expected: All 5 tests pass.

- [ ] **Step 7: Commit**

```bash
git add UpgradedCrawler.Tests/ProviderTests/
git commit -m "test: add parser tests for all 5 providers"
```

---

## Task 19: Write MailgunService tests

**Files:**
- Create: `UpgradedCrawler.Tests/MailgunServiceTests.cs`

`MailgunService` uses `mailgun_csharp` which internally makes HTTP calls. The test verifies the exception behaviour introduced in Task 5 (non-2xx → exception, null response → exception).

Because `MessageService` from `mailgun_csharp` is not easily mockable at the HTTP level, test the public contract: that `SendEmail` does NOT throw on a successful send and DOES throw on failure. Use `NSubstitute` to stub `IEmailService` itself for any tests that consume the service, and use a lightweight integration approach for the actual `MailgunService` unit tests by injecting a broken options object.

```csharp
using Microsoft.Extensions.Options;
using UpgradedCrawler.Core.Entities;
using UpgradedCrawler.Service;

namespace UpgradedCrawler.Tests;

public class MailgunServiceTests
{
    [Fact]
    public async Task SendEmail_WithInvalidDomain_ThrowsHttpRequestException()
    {
        // Mailgun with a bad API key/domain will return 401 or similar — EnsureSuccessStatusCode throws.
        var options = Options.Create(new MailgunOptions
        {
            ApiKey = "invalid-key",
            Domain = "invalid.domain.test",
            TemplateName = "test-template",
            To = "to@example.com",
            FromAddress = "from@example.com",
            FromName = "Test"
        });

        var service = new MailgunService(options);
        var assignments = new List<AssignmentAnnouncement>
        {
            new("id-1", "https://example.com", "upgraded", "Test", DateTime.Now)
        };

        await Assert.ThrowsAnyAsync<Exception>(() =>
            service.SendEmail("from@example.com", "Test", "to@example.com", "Subject", assignments));
    }
}
```

> Note: This test makes a real outbound call to Mailgun's API and expects a failure. If you want a fully offline test, refactor `MailgunService` to accept an `IMessageService` abstraction — that is left as optional future work.

- [ ] **Step 1: Create MailgunServiceTests.cs** (content as above)

- [ ] **Step 2: Run all tests**

```bash
dotnet test UpgradedCrawler.Tests/UpgradedCrawler.Tests.csproj
```

Expected: All tests pass.

- [ ] **Step 3: Commit**

```bash
git add UpgradedCrawler.Tests/MailgunServiceTests.cs
git commit -m "test: add MailgunService exception-on-failure test"
```

---

## Task 20: Fix remaining nullable warnings

After enabling `<Nullable>enable</Nullable>` in the main project in Task 1, nullable warnings need to be fixed. This task addresses any that surfaced.

**Files:**
- Modify: Any files in `UpgradedCrawler/` that have nullable warnings

- [ ] **Step 1: Check for warnings**

```bash
dotnet build 2>&1 | grep -i warning
```

- [ ] **Step 2: Fix each warning**

Common patterns to fix:
- `string?` return where `string` is expected → add `?? string.Empty` or null-check
- `IHttpClientFactory` parameter warning → already typed as non-nullable in constructors, should be fine
- `DbSet<AssignmentAnnouncement>?` in `AppDbContext` → add `!` operator where used after `EnsureCreated` (e.g., `db.Assignments!.Any(...)` — already done in base class)

- [ ] **Step 3: Verify clean build**

```bash
dotnet build
```

Expected: Build succeeded with 0 errors, 0 warnings.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "fix: resolve nullable warnings after enabling Nullable in main project"
```

---

## Task 21: Verification

- [ ] **Step 1: Full build**

```bash
dotnet build
```

Expected: 0 errors, 0 warnings across all projects.

- [ ] **Step 2: Full test run**

```bash
dotnet test UpgradedCrawler.Tests/UpgradedCrawler.Tests.csproj --verbosity normal
```

Expected: All tests pass. Output shows each test name and "Passed".

- [ ] **Step 3: Smoke run**

```bash
dotnet run --project UpgradedCrawler/UpgradedCrawler.csproj -- -f
```

Expected: App starts, logs "Fetching assignments from 'upgraded'..." for each provider, logs counts, exits cleanly. If `appsettings.local.json` is not present, expect `InvalidOperationException: Mailgun ApiKey is not configured` — that is correct startup validation behaviour.

- [ ] **Step 4: Confirm appsettings.local.template.json is complete**

Open `UpgradedCrawler/appsettings.local.template.json` and verify it contains all required keys: `mailgun.*`, `MissPrym.ApiKey`, `Notification.Enabled`, `Providers`.

- [ ] **Step 5: Final commit (if any loose changes)**

```bash
git status
```

If clean, nothing to do. If there are uncommitted changes from the nullable fix pass, commit them.
