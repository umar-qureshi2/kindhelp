---
description: Run a privacy review against the donor-anonymity invariant
---

Audit the current change set (or the named files) for violations of the KindHelp privacy invariant documented in CLAUDE.md.

For each change, check:

1. Does any public-facing query (under `Pages/`, anything not in `Pages/My/` or `Pages/Admin/`) project `Contribution.DonorUserId`, `Donor`, `Donor.Email`, `Donor.DisplayName`, or `RecordedByUserId`? If yes, flag it.
2. Does any `/My/*` PageModel derive its user id from anywhere other than `UserManager.GetUserId(User)`? Query string, route value, form post, hidden input, cookie — all disqualifying. If yes, flag it.
3. Does any new aggregate count rows instead of `DISTINCT` donor ids? Repeat donations should not inflate supporter counts.
4. Does any new view template render donor identifiers outside `Pages/Admin/`? Search for `Donor.`, `DisplayName`, `Email`, `UserName` in cshtml files outside Admin.

Report each finding with file path, line number, and the smallest fix that preserves the feature. If everything is clean, say so explicitly.
