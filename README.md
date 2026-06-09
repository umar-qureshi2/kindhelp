# KindHelp

A small, low-cost charity case platform with a privacy-first donor model.

- **Public side**: visitors browse charity cases with full background, photos, updates, and aggregate totals.
- **Donor side**: each contributor sees only their own contributions and updates from cases they supported. Donors are never visible to other donors.
- **Admin side**: a single admin role manages cases, photos, updates, and records contributions against donor accounts.

## Tech stack

- ASP.NET Core 8 (Razor Pages)
- PostgreSQL via Npgsql + EF Core 8
- ASP.NET Core Identity (Admin / Donor roles)
- nginx + Kestrel + systemd on a single Linux VM
- Free TLS via Let's Encrypt (certbot)

No payment gateway in v1. Donations are received offline (bank transfer, mobile wallet, cash) and recorded by an admin against the donor's account.

## Repository layout

```
KindHelp.sln
src/
  KindHelp.Web/            ASP.NET Core Razor Pages app (everything lives here)
    Data/                  EF Core DbContext + seed
    Models/                Domain entities
    Services/              CaseService, ContributionService, file storage, slug helper
    Pages/
      Account/             Login, Register, Logout, AccessDenied
      Cases/               Public case list + detail
      My/                  Donor dashboard + contribution log (per-user filtered)
      Admin/               Admin dashboard, case CRUD, photos, updates, contributions
deploy/
  kindhelp.service         systemd unit
  nginx.conf               reverse proxy site
  env.sample               environment file (becomes /etc/kindhelp/env)
```

## Running locally (Windows, one command)

From an **elevated PowerShell** in the project root:

```
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\setup.ps1     # installs .NET 8 SDK + PostgreSQL via winget, creates the DB, runs the initial migration
.\run.ps1       # starts the app and opens http://localhost:5080
```

`setup.ps1` is idempotent — re-run it any time. It will prompt for the `postgres` superuser password you set during the PostgreSQL install (only needed once, the first time).

A default admin is seeded with `admin@kindhelp.local` / `ChangeMe!2026` (configured in `appsettings.json`). **Change these before deploying.**

### Manual setup (any OS)

Prerequisites: .NET 8 SDK, PostgreSQL 14+ available locally.

```
# 1. Create the database
psql -U postgres -c "CREATE USER kindhelp WITH PASSWORD 'changeme';"
psql -U postgres -c "CREATE DATABASE kindhelp OWNER kindhelp;"

# 2. Restore + run (migrations and seed run automatically on startup)
cd src/KindHelp.Web
dotnet restore
dotnet ef migrations add Initial  # one-time, on first checkout
dotnet run
```

Visit http://localhost:5080.

## Deploying to a single Linux VM (cheapest path)

Goal: one small VM (1 vCPU, 1 GB RAM is enough for low traffic), Postgres on the same box, nginx as the front door, free Let's Encrypt cert. No managed services, no per-call fees.

### 1. Provision

```
sudo apt update
sudo apt install -y nginx postgresql postgresql-contrib certbot python3-certbot-nginx
# .NET 8 runtime
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O /tmp/ms.deb
sudo dpkg -i /tmp/ms.deb && sudo apt update
sudo apt install -y aspnetcore-runtime-8.0
```

### 2. Database

```
sudo -u postgres psql -c "CREATE USER kindhelp WITH PASSWORD 'STRONG_PASSWORD';"
sudo -u postgres psql -c "CREATE DATABASE kindhelp OWNER kindhelp;"
```

### 3. Publish from your dev machine

```
dotnet publish src/KindHelp.Web/KindHelp.Web.csproj -c Release -o publish
scp -r publish/* youruser@yourvm:/tmp/kindhelp
```

On the VM:

```
sudo mkdir -p /var/www/kindhelp
sudo cp -r /tmp/kindhelp/* /var/www/kindhelp/
sudo chown -R www-data:www-data /var/www/kindhelp
sudo mkdir -p /var/www/kindhelp/wwwroot/uploads
sudo chown -R www-data:www-data /var/www/kindhelp/wwwroot/uploads
```

### 4. Environment file

```
sudo mkdir -p /etc/kindhelp
sudo cp deploy/env.sample /etc/kindhelp/env
sudo nano /etc/kindhelp/env   # set real connection string + admin credentials
sudo chmod 600 /etc/kindhelp/env
```

### 5. systemd service

```
sudo cp deploy/kindhelp.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now kindhelp
sudo systemctl status kindhelp
```

### 6. nginx + free TLS

```
sudo cp deploy/nginx.conf /etc/nginx/sites-available/kindhelp
# Edit server_name to your real domain
sudo ln -s /etc/nginx/sites-available/kindhelp /etc/nginx/sites-enabled/kindhelp
sudo nginx -t && sudo systemctl reload nginx
sudo certbot --nginx -d yourdomain.example.org
```

That's it — the app is now live with TLS at zero recurring software cost.

## Privacy model (read this before extending)

The donor-anonymity rule is enforced in code:

- `ICaseService` only ever returns aggregate totals (`SUM(Amount)`, `COUNT(DISTINCT donor)`) on public views. There is no method that returns donor identities for a public page.
- `IContributionService.GetMyCaseSummariesAsync` and `GetMyContributionsAsync` accept a `userId` and filter strictly to that user. The user id is always taken from `UserManager.GetUserId(User)`, never from the request.
- The `/My` folder is `[Authorize]` and the `/Admin` folder requires the `AdminOnly` policy.
- The donor-only endpoints never join across other donors' rows, so a SQL bug cannot accidentally leak.

If you add a feature, never expose `Contribution.DonorUserId`, donor name, or donor email outside admin views.

## Backups (free, simple)

A nightly `pg_dump` to a second cheap location is the smallest reasonable backup:

```
0 3 * * *  /usr/bin/pg_dump -U kindhelp kindhelp | gzip > /var/backups/kindhelp-$(date +\%F).sql.gz
```

Sync `/var/backups` and `/var/www/kindhelp/wwwroot/uploads` to a free-tier object store (e.g. Backblaze B2 first 10 GB free) using `rclone`.

## Roadmap (out of scope for v1)

- Online payments via Stripe (per-transaction fees only)
- Email notifications on case updates (Brevo / Mailjet free tier)
- Bulk import of historical contributions
- Optional public "thank-you wall" with explicit per-donor opt-in (currently disallowed by design)

## License

Use freely for charitable purposes.
