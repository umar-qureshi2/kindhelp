# KindHelp — Claude Code instructions

This file is read automatically by Claude Code at the start of every session. Keep it accurate.

## What this project is

KindHelp is a privacy-first charity platform. Admins publish charity cases with full background and photos and post updates as work progresses. Donors register, contribute offline (bank transfer, mobile wallet, cash), and an admin records each contribution against the donor's account. Each donor sees a private dashboard with their own activity. Public pages show only aggregate totals.

Hosted on a single Linux VM with no managed services. Cost-minimisation is a deliberate constraint.

## The privacy invariant — read this before changing anything

**Donors must never be visible to other donors.** Public pages and any cross-donor view may show only:

- the SUM of contribution amounts per case
- the COUNT of distinct donors per case

Public pages must NEVER expose:

- `Contribution.DonorUserId`
- `ApplicationUser.UserName` / `Email` / `DisplayName`
- any per-donation amount tied to a specific donor

The donor's own activity is visible only at `/My/*` and only to that donor. Admin views at `/Admin/*` are the single place where individual donor records appear.

This is enforced in code, not just in policy:

- `ICaseService` has no method that returns donor identities for a public page.
- `IContributionService.GetMyCaseSummariesAsync` / `GetMyContributionsAsync` accept a `userId` and filter strictly to that user.
- All `/My/*` PageModels derive the user id from `UserManager.GetUserId(User)`. Never accept a user id from the query string, route, or form.
- `Pages/My` is `[Authorize]`. `Pages/Admin` is `[Authorize(Policy = "AdminOnly")]`. The folder-level convention is set in `Program.cs`.

If you add a feature, do not weaken any of the above without explicit user direction.

## Tech stack

- ASP.NET Core 8, Razor Pages
- PostgreSQL via Npgsql + EF Core 8 (migrations + seed run automatically on startup)
- ASP.NET Core Identity, two roles: `Admin`, `Donor`
- Plain CSS in `wwwroot/css/site.css` (no Tailwind, no build step, no CDN)
- nginx + Kestrel + systemd for production; free TLS via Let's Encrypt

No payment gateway is wired up and there is no plan to add one in v1. Donations are recorded manually by an admin.

## Folder layout

```
KindHelp.sln
src/KindHelp.Web/
  Program.cs                   composition root + pipeline + folder-level authorization
  Data/
    ApplicationDbContext.cs    EF Core context (extends IdentityDbContext<ApplicationUser>)
    DbInitializer.cs           seeds roles + a default admin on first run
  Models/
    ApplicationUser.cs         IdentityUser + DisplayName, ContactNotes
    Case.cs, CaseUpdate.cs, CasePhoto.cs, Contribution.cs, Roles.cs
  Services/
    ICaseService / CaseService                  public + admin case projections
    IContributionService / ContributionService  per-user privacy-filtered queries
    IFileStorageService / LocalFileStorageService  saves uploads under wwwroot/uploads/cases/{id}/
    Slugger.cs                  URL-safe slug helper
  Pages/
    Index.cshtml, About.cshtml          public landing
    Account/                            Login, Register, Logout, AccessDenied
    Cases/                              public case Index + Details ("/cases/{slug}")
    My/                                 [Authorize] donor dashboard + contributions log
    Admin/                              [Authorize(Policy="AdminOnly")] case CRUD, photos, updates, contributions
    Shared/_Layout.cshtml, _ValidationScriptsPartial.cshtml
  wwwroot/css/site.css                  single stylesheet
  wwwroot/uploads/                      runtime image storage (gitignored)
deploy/
  kindhelp.service, nginx.conf, env.sample
README.md                               full deployment guide
```

## Build and run

On Windows, the bundled scripts are the easy path:

```
# elevated PowerShell, project root
.\setup.ps1     # one-time: installs .NET 8 SDK + PostgreSQL via winget, creates DB, runs initial migration
.\run.ps1       # starts http://localhost:5080
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

- Page directory protection is set in `Program.cs` via `Conventions.AuthorizeFolder`. Don't move auth from there into per-page attributes — keep it centralized so it's easy to audit.
- New donor-facing pages go under `Pages/My/`. New admin-facing pages go under `Pages/Admin/`. Public pages live at the top of `Pages/` or under `Pages/Cases/`.
- Service methods that return data for public pages must return aggregates, not entities containing donor identifiers.
- Use the existing `Slugger.Slugify` for any new URL-safe identifier.
- HTML in case `Description` and `CaseUpdate.Body` is encoded with `WebUtility.HtmlEncode` before rendering (see `Pages/Cases/Details.cshtml`). Do not switch to `Html.Raw` on raw user input.
- File uploads are restricted by extension via `KindHelp:AllowedImageExtensions` and size via `KindHelp:MaxUploadMb`. Keep both configurable; do not hardcode.
- EF Core 8 + Npgsql is the target — `ExecuteUpdateAsync`/`ExecuteDeleteAsync` are fine to use; both are wired in already (see `Pages/Admin/Cases/Photos.cshtml.cs`).

## Things explicitly not to do

- Don't add a public donor list, leaderboard, or "thank-you wall." This is a deliberate product decision.
- Don't add Stripe / PayPal / any payment gateway in v1. Donations are recorded by admins only.
- Don't introduce a JS framework, npm build, or CDN dependency for the front end. Keep it server-rendered with the existing single CSS file.
- Don't switch the database or move to a managed service without checking with the user — the cost constraint is real.

## What's already done in v1

- Public case browse + detail with photos, full history, update timeline, aggregate raised panel
- Donor registration / login / logout
- Donor dashboard: cases supported, contributions log, recent updates from supported cases
- Admin: case CRUD, photo upload with cover selection, post/delete updates, record/delete contributions tied to a donor account
- Auto-migrate + seed default admin and roles on startup
- Deployment kit: systemd unit, nginx config, env file template, README with VM bring-up steps

## Likely next tasks (when the user resumes)

- Generate the initial EF migration (`dotnet ef migrations add Initial`) and verify the schema against `ApplicationDbContext`.
- Add a small seed of sample cases for local clicking-around.
- Optional email notifications on case updates (free SMTP tier; opt-in per donor).
- Per-page `<title>`/Open Graph metadata for case detail.
- Rate-limiting on Login/Register (Identity has lockout already; consider adding `IpRateLimit`).

## Privacy review checklist (run mentally before shipping any change touching donor data)

1. Does this code path return donor identifiers in a context that anyone other than that donor (or an admin) can reach? If yes, fix it.
2. Is the user id derived from `UserManager.GetUserId(User)`? If it comes from a parameter, route, or form, fix it.
3. Does any new public projection select `DonorUserId`, `Donor`, `Donor.Email`, `Donor.DisplayName`, or `RecordedByUserId`? If yes, remove.
4. Are aggregate counts using `DISTINCT` on the donor id rather than counting rows? (A donor can give multiple times; they're still one supporter.)
