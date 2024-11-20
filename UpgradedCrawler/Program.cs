using System.Text.Json;
using HtmlAgilityPack;
using UpgradedCrawler;
using System.Text.RegularExpressions;
using UpgradedCrawler.Core.Entities;
using Microsoft.Extensions.Configuration;
using UpgradedCrawler.Service;

const string websiteUrl = "https://upgraded.se/lediga-uppdrag/";
const string adminUrl = "https://upgraded.se/wp-admin/admin-ajax.php";
var csvFilePath = "records.csv";
var newRecordsFile = "new_records.csv";

try
{
    var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false)
        .Build();
    var mailgunOptions = configuration.GetSection("mailgun").Get<MailgunOptions>();
    var emailService = new MailgunService(mailgunOptions);

    if (!IsWorkingHour())
    {
        Log("It's not working hours. The program will exit.");
        return;
    }
    if (args.Length > 0)
    {
        csvFilePath = $"{args[0]}/{csvFilePath}";
        newRecordsFile = $"{args[0]}/{newRecordsFile}";
    }
    var records = new Dictionary<string, string>();

    if (File.Exists(csvFilePath))
    {
        records = await GetRecordsFromFile(csvFilePath);
    }

    static async Task<Dictionary<string, string>> GetRecordsFromFile(string path)
    {
        var lines = await File.ReadAllLinesAsync(path);
        var records = new Dictionary<string, string>();
        foreach (var line in lines.Skip(1)) // Skip header
        {
            var parts = line.Split(',');
            if (parts.Length >= 2)
            {
                var id = parts[0];
                var urlValue = string.Join(",", parts.Skip(1)).Trim('"');
                records[id] = urlValue;
            }
        }
        return records;
    }
    var nonce = await GetNonce();
    if (string.IsNullOrEmpty(nonce))
    {
        Log("Nonce not found. The program will exit.");
        return;
    }

    using var httpClient = new HttpClient();

    httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/javascript, */*; q=0.01");
    httpClient.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");

    // Prepare the form data
    var formData = new FormUrlEncodedContent(
    [
        new KeyValuePair<string, string>("action", "do_filter_posts"),
        new KeyValuePair<string, string>("nonce", $"{nonce}"),
        new KeyValuePair<string, string>("params[ort-term]", "alla-orter"),
        new KeyValuePair<string, string>("params[roll-term]", "alla-roller"),
        new KeyValuePair<string, string>("params[kund-term]", "alla-kunder")
    ]);

    // Send the POST request
    var response = await httpClient.PostAsync(adminUrl, formData);
    response.EnsureSuccessStatusCode();

    // Read the response as a string
    var responseString = await response.Content.ReadAsStringAsync();

    // Parse the JSON to get the HTML content
    var jsonDoc = JsonDocument.Parse(responseString);
    string htmlContent = jsonDoc.RootElement.GetProperty("content").GetString();

    // Load HTML content into HtmlAgilityPack for parsing
    var htmlDoc = new HtmlDocument();
    htmlDoc.LoadHtml(htmlContent);

    // Extract and display table data
    var rows = htmlDoc.DocumentNode.SelectNodes("//table/tr[position()>1]");

    if (rows?.Count == 0)
    {
        Log("No data rows found in the table.");
        return;
    }

    var newRecords = new Dictionary<string, string>();

    rows?.ToList().ForEach(row =>
    {
        var url = row.SelectSingleNode("td[1]/a")?.GetAttributeValue("href", "") ?? "";
        var id = row.SelectSingleNode("td[5]").InnerText.Trim();
        if (records.TryAdd(id, url))
        {
            newRecords.Add(id, url);
        }
    });

    if (newRecords.Count == 0)
    {
        Log("No new records found.");
        return;
    }
    else
    {
        var suffix = newRecords.Count == 1 ? "" : "s";
        await emailService.SendEmail("aziz.khakulov@bitfinity.dev", $"New Assignment Announcement{suffix} on Upgraded People", GetAssignmentAnnouncements(newRecords));
    }

    await WriteToFile(records, csvFilePath);
    await WriteToFile(newRecords, newRecordsFile);
}
catch (Exception ex)
{
    Log($"An error occurred: {ex.Message}");
}

/// <summary>
/// Checks if the current time is within working hours.
/// </summary>
static bool IsWorkingHour()
{
    var now = DateTime.Now;
    return now.DayOfWeek != DayOfWeek.Saturday && now.DayOfWeek != DayOfWeek.Sunday && now.Hour >= 8 && now.Hour < 17;
}
/// <summary>
/// Logs a message to the console with a timestamp.
/// </summary>
/// <param name="message">The message to log.</param>
static void Log(string message)
{
    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
}

/// <summary>
/// Writes records to a CSV file.
/// </summary>
/// <param name="records">The records to write.</param>
/// <param name="csvFilePath">The path to the CSV file.</param>
static async Task WriteToFile(Dictionary<string, string> records, string csvFilePath)
{
    var csvLines = new List<string> { "id,url" };
    foreach (var record in records)
    {
        // Escape commas in URLs if necessary
        string escapedUrl = record.Value.Contains(",") ? $"\"{record.Value}\"" : record.Value;
        csvLines.Add($"{record.Key},{escapedUrl}");
    }
    await File.WriteAllLinesAsync(csvFilePath, csvLines);
    Log($"{records.Count} records written to {csvFilePath}.");
}

/// <summary>
/// Gets nonce from the website.
/// </summary>
static async Task<string> GetNonce()
{
    using var httpClient = new HttpClient();
    var response = await httpClient.GetAsync(websiteUrl);
    response.EnsureSuccessStatusCode();

    var content = await response.Content.ReadAsStringAsync();

    // Define a regex pattern to match the nonce value
    var noncePattern = @"var\s+bobz\s*=\s*\{\s*""nonce""\s*:\s*""(?<nonce>\w+)""";
    var match = Regex.Match(content, noncePattern);

    if (match.Success && match.Groups["nonce"].Success)
    {
        return match.Groups["nonce"].Value;
    }
    else
    {
        Log("Nonce not found in the response.");
        return string.Empty;
    }
}

/// <summary>
/// Converts a dictionary of records to a list of AssignmentAnnouncement objects.
/// </summary>
/// <param name="records">The dictionary of records to convert.</param>
/// <returns>A list of AssignmentAnnouncement objects.</returns>
static List<AssignmentAnnouncement> GetAssignmentAnnouncements(Dictionary<string, string> records)
{
    return records.Select(r => new AssignmentAnnouncement(r.Key, r.Value)).ToList();
}