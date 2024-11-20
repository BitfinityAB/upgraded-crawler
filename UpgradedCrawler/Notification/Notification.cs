using System.Diagnostics;

namespace UpgradedCrawler
{

    internal static class Notification
    {
        internal static void ShowMacNotification(string title, string message)
        {
            // Escape double quotes for the osascript command
            string escapedTitle = title.Replace("\"", "\\\"");
            string escapedMessage = message.Replace("\"", "\\\"");

            var process = new ProcessStartInfo
            {
                FileName = "osascript",
                Arguments = $"-e \"display notification \\\"{escapedMessage}\\\" with title \\\"{escapedTitle}\\\"\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(process);
            proc.WaitForExit();
        }
    }
}