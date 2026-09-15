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

If AI matching was misconfigured for a while (e.g. a missing `Matching.ProfileFolder`) and assignments
piled up unanalyzed, run a one-off backfill once the config is fixed. This loads every assignment
already stored in the database, skips fetching/announcement email, and runs Phase 2 matching only on
the ones without an analysis record yet:
```bash
sudo -u deploy /opt/upgraded-crawler/UpgradedCrawler --rematch-backlog
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
