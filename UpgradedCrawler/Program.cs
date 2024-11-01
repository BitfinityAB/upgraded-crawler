using System.Text.Json;
using HtmlAgilityPack;
using UpgradedCrawler;

const string url = "https://upgraded.se/wp-admin/admin-ajax.php";
var csvFilePath = "records.csv";
var newRecordsFile = "new_records.csv";

try
{
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

    using var httpClient = new HttpClient();

    httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/javascript, */*; q=0.01");
    httpClient.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");

    // Prepare the form data
    var formData = new FormUrlEncodedContent(
    [
        new KeyValuePair<string, string>("action", "do_filter_posts"),
        new KeyValuePair<string, string>("nonce", "29a3b01dc3"),
        new KeyValuePair<string, string>("params[ort-term]", "alla-orter"),
        new KeyValuePair<string, string>("params[roll-term]", "alla-roller"),
        new KeyValuePair<string, string>("params[kund-term]", "alla-kunder")
    ]);

    // Send the POST request
    var response = await httpClient.PostAsync(url, formData);
    response.EnsureSuccessStatusCode();

    // Read the response as a string
    var responseString = await response.Content.ReadAsStringAsync();

    // Parse the JSON to get the HTML content
    var jsonDoc = JsonDocument.Parse(responseString);
    string htmlContent = jsonDoc.RootElement.GetProperty("content").GetString();

    // Load HTML content into HtmlAgilityPack for parsing
    var htmlDoc = new HtmlDocument();
    htmlDoc.LoadHtml(htmlContent);

    // Extract and display table data //*[@id="container-async"]/div[2]/table/tbody/tr[2]/td[5]
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
        Notification.ShowMacNotification("New assignment(s)", $"{newRecords.Count} new assignment(s) were found on Upgraded's website.");
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
    return now.DayOfWeek != DayOfWeek.Saturday && now.DayOfWeek != DayOfWeek.Sunday && now.Hour >= 9 && now.Hour < 17;
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