# upgraded-crawler

Simple .NET console app for monitoring the assignments on Upgraded People's website.

## Usage

1. Compile the app using `dotnet build` command.
2. Copy `appsettings.local.template.json` to `appsettings.local.json` and fill in your configuration values.
3. For macOS: read [here](https://alvinalexander.com/mac-os-x/mac-osx-startup-crontab-launchd-jobs/) about creating plist files for `launchctl`. Create a file in the location suitable for you.
   For Windows: schedule the program to run every hour in Windows Task Scheduler.

Recommended to run the script every hour at most frequent to avoid being blacklisted from the service.

## Configuration

The application uses two configuration files:
- `appsettings.json`: Contains default and non-sensitive settings
- `appsettings.local.json`: Contains sensitive settings like API keys and email addresses (not committed to git)

To set up your local configuration:
1. Copy `appsettings.local.template.json` to `appsettings.local.json`
2. Fill in your Mailgun API key, domain, and email address in `appsettings.local.json`
