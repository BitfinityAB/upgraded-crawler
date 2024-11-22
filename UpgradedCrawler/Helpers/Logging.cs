namespace UpgradedCrawler.Helpers
{
    public static class Logging
    {
        /// <summary>
        /// Logs a message to the console with a timestamp.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void Log(string message)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
        }
    }
}