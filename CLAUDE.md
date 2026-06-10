# KindHelp — Claude Code instructions

This file is read automatically by Claude Code at the start of every session. Keep it accurate.

## What this project is

KindHelp is a privacy-first charity platform with a **wallet model**. Admins publish charity cases with full background and photos and post updates as work progresses. People give via offline channels (bank transfer, mobile wallet, cash); admins record those receipts as **deposits** into per-donor **wallets**. Cases are funded by **allocating** from wallets — one bulk deposit can be spread across multiple cases over time.

Donors can either register an account themselves, OR an admin can create a lightweight admin-managed donor profile on the fly using just an email or phone number. Wallets work the same either way.

Hosted on a single Linux VM with no managed services. Cost-minimisation is a deliberate constraint.

## The privacy invariant — read this before changing anything

**Donors must never be visible to other donors.** Public pages and any cross-donor view may show only:

- the SUM of contribution amounts per case
- the COUNT of distinct donors per case (`COUNT(DISTINCT DonorId)`)

Public pages must NEVER expose:

- `Contribution.DonorId` (or donor's name / email / phone)
- `Donor.DisplayName` / `Email` / `PhoneNumber`
- `WalletTransaction` rows
- any per-donation amount tied to a specific donor

A registered donor sees their own activity only at `/My/*` and only for their own `Donor` row (resolved server-side from `UserManager.GetUserId(User)`). Admin-managed donors (no login) have no `/My/*` access — they're administered only from `/Admin/Donors/*`. Admin pages at `/Admin/*` are the single place where individual donor records appear.

This is enforced in code:

- `ICaseService` never returns donor identities for a public page. Counts are `Select(x => x.DonorId).Distinct().Count()`.
- `IContributionService` methods take a `donorId` (NOT an `ApplicationUser` id) and filter strictly. The donorId is always derived from `IDonorService.EnsureForUserAsync(user)` inside `/My/*` PageModels.
- All `/My/*` PageModels derive the user from `UserManager.GetUserAsync(User)`. Never accept a user/donor id from query string, route, or form.
- `Pages/My` is `[Authorize]`. `Pages/Admin` is `[Authorize(Policy = "AdminOnly")]`. The folder-level convention is set in `Program.cs`.

If you add a feature, do not weaken any of the above without explicit user direction.

## Tech stack

- ASP.NET Core 8, Razor Pages
- PostgreSQL via Npgsql + EF Core 8 (migrations + seed run automatically on startup)
- ASP.NET Core Identity, two roles: `Admin`, `Donor`
- Plain CSS in `wwwroot/css/site.css` — full custom-property design system with dark/light themes (system-aware + manual toggle stored in `localStorage`). No Tailwind, no build step, no CDN.
- nginx + Kestrel + systemd for production; free TLS via Let's Encrypt

No payment gateway is wired up and there is no plan to add one in v1. Donations are recorded manually by an admin.

## Domain model (post-wallet refactor)

- **`Donor`** — one row per giving party, registered or admin-managed. Holds `WalletBalance` (denormalised cache of SUM(WalletTransactions.Amount)). Optional `ApplicationUserId` link.
- **`WalletTransaction`** — append-only audit log. Types: `Deposit`, `Allocation`, `Refund`, `Adjustment`. Signed `Amount` + `BalanceAfter` snapshot.
- **`Contribution`** — money allocated from a donor's wallet to a case. Each contribution is created by exactly one `WalletTransaction` of type `Allocation` (two-way FK).
- **`Case` / `CaseUpdate` / `CasePhoto`** — unchanged.

Two distinct admin operations live in `WalletService`, each wrapped in a DB transaction:

- **`DepositAsync`** — credits a wallet. Used when bulk money arrives.
- **`AllocateAsync`** — debits a wallet, creates a `Contribution`, links them. Throws `InsufficientWalletBalanceException` if balance is too low (admin is sent to deposit first; no auto-deposit, no overdraft).

## Folder layout

```
KindHelp.sln
src/KindHelp.Web/
  Program.cs                   composition root + pipeline + folder-level authorization
  Data/
    ApplicationDbContext.cs    EF Core context (extends IdentityDbContext<ApplicationUser>)
    DbInitializer.cs           seeds roles + a default admin on first run
  Models/
    ApplicationUser.cs         IdentityUser + DisplayName, ContactNotes, Donor nav
    Case.cs, CaseUpdate.cs, CasePhoto.cs, Roles.cs
    Donor.cs, WalletTransaction.cs, Contribution.cs
  Services/
    ICaseService / CaseService                     public + admin case projections
    IContributionService / ContributionService     per-donor privacy-filtered queries
    IDonorService / DonorService                   donor lookup, create, edit, link-to-user
    IWalletService / WalletService                 deposit, allocate, adjust, history; transactional
    IFileStorageService / LocalFileStorageService  saves uploads under wwwroot/uploads/cases/{id}/
    Slugger.cs                                     URL-safe slug helper
  Pages/
    Index.cshtml, About.cshtml             public landing
    Account/                               Login, Register (auto-creates Donor + wallet), Logout, AccessDenied
    Cases/                                 public case Index + Details ("/cases/{slug}")
    My/                                    [Authorize] Dashboard, Contributions, Wallet
    Admin/                                 [Authorize(Policy="AdminOnly")]
      Cases/                                 case CRUD, photos, updates
      Donors/                                Index, Create, Edit, Details, Deposit
      Contributions/                         Index, Create (= allocate from wallet)
    Shared/_Layout.cshtml, _ValidationScriptsPartial.cshtml
  wwwroot/css/site.css                     single stylesheet, custom-property themed
  wwwroot/uploads/                         runtime image storage (gitignored)
deploy/
  kindhelp.service, nginx.conf, env.sample
setup.ps1, run.ps1                         Windows local-dev bootstrap
fix-postgres.ps1, fix-kindhelp-user.ps1,
reset-postgres-password.ps1, reset-db.ps1  Recovery helpers
```

## Build and run

On Windows, the bundled scripts are the easy path:

```
# elevated PowerShell, project root
.\setup.ps1     # one-time: installs .NET 8 SDK + PostgreSQL via winget, creates DB, runs initial migration
.\run.ps1       # starts http://localhost:5080
```

If you've changed the schema (e.g. pulled this wallet refactor onto an existing DB):

```
.\reset-db.ps1  # DESTRUCTIVE: drops local kindhelp DB, deletes Migrations/, regenerates Initial migration
.\run.ps1
```

Manual path (any OS):

```
cd src/KindHelp.Web
dotnet restore
dotnet ef migrations add Initial   # only if /Data/Migrations does not exist yet
dotnet run                         # http://localhost:5080
```

A default admin user is seeded from `KindHelp:DefaultAdminEmail` / `KindHelp:DefaultAdminPassword` in `appsettings.json`. **Change these before deploying.** Local dev credentials are written into `appsettings.Development.json` by `setup.ps1`; this file is gitignored.

## Conventions

- Page directory protection is set in `Program.cs` via `Conventions.AuthorizeFolder`. Don't move auth into per-page attributes — keep it centralized so it's easy to audit.
- New donor-facing pages go under `Pages/My/`. New admin-facing pages go under `Pages/Admin/`. Public pages live at the top of `Pages/` or under `Pages/Cases/`.
- Service methods that return data for public pages must return aggregates, not entities containing donor identifiers.
- Wallet movement always goes through `IWalletService` so the balance cache and the audit log stay in lock-step inside a DB transaction.
- Use the existing `Slugger.Slugify` for any new URL-safe identifier.
- HTML in case `Description` and `CaseUpdate.Body` is encoded with `WebUtility.HtmlEncode` before rendering. Do not switch to `Html.Raw` on raw user input.
- File uploads are restricted by extension via `KindHelp:AllowedImageExtensions` and size via `KindHelp:MaxUploadMb`. Keep both configurable.
- CSS uses custom properties (`--bg`, `--text`, `--brand`, etc.) so both themes work. Any new component must reference these tokens; never hard-code colours.
- EF Core 8 + Npgsql is the target — `ExecuteUpdateAsync`/`ExecuteDeleteAsync` are fine to use.

## Things explicitly not to do

- Don't add a public donor list, leaderboard, or "thank-you wall." Deliberate product decision.
- Don't add Stripe / PayPal / any payment gateway in v1. Donations are recorded by admins only.
- Don't allow wallet balances to go negative without an explicit `Adjustment`. `AllocateAsync` blocks when balance < amount.
- Don't introduce a JS framework, npm build, or CDN dependency for the front end. Keep it server-rendered with the existing single CSS file.
- Don't switch the database or move to a managed service without checking with the user.

## What's done in v2 (this wallet refactor)

- `Donor` + `WalletTransaction` entities with full audit trail
- Admin can create donors inline during contribution recording, with just email or phone
- Bulk deposits accumulate in wallet; allocations spread across cases over time
- Donor `/My/Wallet` shows balance + per-transaction history with linked cases
- Auto-create / claim Donor row on user registration (links existing admin-managed profile if email matches)
- Contribution deletion now reverses the wallet movement with a compensating Adjustment
- Modern UI with system-aware dark/light themes and a manual toggle in the header

## Privacy review checklist (run mentally before shipping any change touching donor data)

1. Does this code path return donor identifiers in a context that anyone other than that donor (or an admin) can reach? If yes, fix it.
2. Is the donor id derived from `IDonorService.EnsureForUserAsync(user)` where `user` came from `UserManager.GetUserAsync(User)`? If donor id arrives via parameter, route, or form, fix it.
3. Does any new public projection select `DonorId`, `Donor`, `Donor.Email`, `Donor.DisplayName`, `Donor.PhoneNumber`, `RecordedByUserId`, or any `WalletTransaction` field? If yes, remove.
4. Are aggregate counts using `DISTINCT` on the donor id rather than counting rows? (A donor can give multiple times; they're still one supporter.)
5. For wallet-touching code: is the change wrapped in `_db.Database.BeginTransactionAsync` so the balance cache and the audit log can't diverge?
