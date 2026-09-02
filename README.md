# upgraded-crawler

Simple .NET console app for monitoring the assignments on Upgraded People's website.

## Usage

### Production (Hetzner)

The app runs hourly on a systemd timer on the Hetzner server. Deployment is automatic via
`.github/workflows/deploy.yml` on every push to `main`. See
`docs/superpowers/specs/2026-09-01-hetzner-deployment-design.md` for the full setup
(OneDrive sync, systemd units, one-time secrets/data migration).

To check status on the server:
```bash
systemctl status upgraded-crawler.timer
journalctl --unit=upgraded-crawler -f
```

### Local development

1. Compile the app using `dotnet build` command.
2. Copy `appsettings.local.template.json` to `appsettings.local.json` and fill in your configuration values.
3. Run manually with `dotnet run --project UpgradedCrawler -- --force` (the `--force` flag bypasses
   the working-hours gate, useful for local testing).

Recommended to run the script every hour at most frequent to avoid being blacklisted from the service.

## Configuration

The application uses two configuration files:
- `appsettings.json`: Contains default and non-sensitive settings
- `appsettings.local.json`: Contains sensitive settings like API keys and email addresses (not committed to git)

To set up your local configuration:
1. Copy `appsettings.local.template.json` to `appsettings.local.json`
2. Fill in your Mailgun API key, domain, and email address in `appsettings.local.json`
