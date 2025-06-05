using System.Diagnostics;
using System.Security;
using UpgradedCrawler.Core.Interfaces;

namespace UpgradedCrawler.Helpers
{
    public class Logging(bool logToEventLog = false) : ILogging
    {
        private bool _logToEventLog = logToEventLog;

        /// <summary>
        /// Logs a message to the console with a timestamp.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public void Log(string message)
        {

            if (_logToEventLog && OperatingSystem.IsWindows())
            {
                if (_logToEventLog && OperatingSystem.IsWindows())
                {
                    try
                    {
                        const string logName = "Application";
                        const string source = "UpgradedCrawler";

                        // Create the event source if it doesn't exist
                        if (!EventLog.SourceExists(source))
                        {
                            EventLog.CreateEventSource(source, logName);
                        }

                        using var eventLog = new EventLog(logName)
                        {
                            Source = source
                        };

                        eventLog.WriteEntry(
                            message,
                            EventLogEntryType.Information,
                            eventID: 0,
                            category: 0);
                    }
                    catch (SecurityException ex)
                    {
                        Console.WriteLine($"Failed to write to event log: {ex.Message}. Run as administrator to write to event log.");
                    }
                }
            }
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
        }
    }
}