---
description: Drop and recreate the local KindHelp database (DESTRUCTIVE)
allowed-tools: Bash(psql:*), Bash(dotnet ef:*)
---

DESTRUCTIVE: This drops the local `kindhelp` database and recreates it empty, then re-runs migrations.

1. Confirm with the user before proceeding.
2. Drop and recreate:
   ```
   psql -U postgres -c "DROP DATABASE IF EXISTS kindhelp;"
   psql -U postgres -c "CREATE DATABASE kindhelp OWNER kindhelp;"
   ```
3. Re-run migrations from `src/KindHelp.Web`:
   ```
   cd src/KindHelp.Web && dotnet ef database update
   ```
4. Remind the user that the default admin will be re-seeded with the credentials in `appsettings.json`.
