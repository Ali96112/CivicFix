# CivicFix — recovery log

**25 Aug 2026.** What was lost in the file restore, what has been written back,
and what is still open. Every claim below was checked against the files on disk.

Companion to `CIVICFIX-WALKS.md`.

---

## Written back to disk

These are already on the machine. **Stop, rebuild and restart the API** before the
C# changes take effect — .NET does not hot-reload the way Django's dev server does.

### 1. A report with one baladiye now has a handler

`CivicFix.Api/Controllers/ReportsController.cs` — `CreateReport`, Step 3

```csharp
bool ownedOnCreate = reporterRole == "Staff" || municipalities.Count() == 1;
```

It used to be `reporterRole == "Staff" ? 1 : 0`, so every resident report started
unowned — even when the spatial query matched exactly one baladiye and there was
nothing to decide.

Why it was stuck: `GetSharedReports` only lists reports with 2+ assignment rows, so a
single-baladiye report never reached the Admin's Shared tab. Nothing in the client
called accept. `UpdateReportStatus` only back-fills a handler on "Resolved". So the
detail page read "not handling" for the report's entire life.

### 2. Daily limit of three reports per resident

`CivicFix.Api/Controllers/ReportsController.cs` — restored just before the duplicate check.

One deliberate change: it now counts using `currentUserId` from the token instead of
`request.ReporterId`. The body is whatever the caller typed, so counting on it let
someone dodge their own limit by sending a different id. `CIVICFIX-WALKS.md` states
the rule — read the Id from the token, never from the request body.

### 3. AssignHandler validates the chosen baladiye again

`CivicFix.Api/Controllers/ReportsAdminController.cs` — restored as Step 3.

The step numbering ran Step 1, Step 2, then jumped to Step 4. The missing guard:

```csharp
if (await _connection.ExecuteScalarAsync<int>(
        @"SELECT COUNT(*) FROM tbl_ReportAssignments
          WHERE rpa_ReportId = @ReportId AND rpa_MunicipalityId = @MunicipalityId",
        new { ReportId = id, MunicipalityId = request.MunicipalityId }) == 0)
    return BadRequest("That baladiye is not one of the baladiyat assigned to this report.");
```

**This was the dangerous one.** Step 5 deletes every assignment row that is not the
chosen baladiye; Step 6 updates the chosen one. Pass an id that was never assigned to
that report and the delete removes *all* rows while the update matches none. The
report ends with zero assignments, disappears from every list query (they all
`INNER JOIN tbl_ReportAssignments`), and can never be resolved or recovered through
the UI. The transaction commits and the response says it worked.

### 4. Forgot-password link on the login card

`CivicFix-Client/src/components/auth/LoginForm.jsx` and `src/styles/Login.css`

The `/forgot-password` route, screen and API endpoint all existed. Nothing linked to
them, so no user could reach the feature.

### 5. Welcome page navbar, footer and a Features section

`CivicFix-Client/src/components/WelcomePage.jsx`

- **Navbar** — Features and How it works always; My Reports and Dashboard only when
  logged in. Page-scroll links first, app links second.
- **Footer** — Features, How it works, Dashboard, Report a Problem, then Privacy and
  Contact last. Contact is a `mailto:`; Privacy is parked with `preventDefault()`.
- **New Features section** — six tiles, each one something the backend genuinely does.
  Placed above the steps section so the links walk down the page in order. Reuses
  `.step-card`, so no CSS changes.
- **One `goToReport` helper** — the token check appeared in three places and can no
  longer drift apart. Both `/Report` capitals corrected to `/report`.

### 6. User blocking, rebuilt

`CivicFix.Api/Controllers/UsersController.cs` — 14,182 bytes yesterday, 12,426 after
the restore.

Two pieces had vanished together and both are back:

- the check inside `Login` — placed *after* the password check on purpose, so it
  can't be used to discover which emails belong to blocked accounts
- `PUT api/Users/{id}/block` → `BlockUser(int id)`, Admin only

Blocking does two things in one transaction: the account can no longer log in, and
every report that user filed is removed — children first, reports last, matching the
order `DeleteReport` uses, because the foreign keys are Restrict. Points a baladiye
earned for a deleted report are taken back, so the leaderboard doesn't keep credit
for work that no longer exists.

**Written fresh, not recovered verbatim.** The original couldn't be read back, so this
is a rebuild from the surviving evidence. Two guards were added that may not have been
in the original: an Admin account can't be blocked, and blocking an already-blocked
user is rejected. Their comments and votes on *other* people's reports are deliberately
left alone — deleting those would mean recalculating `rpt_AgreementCount` and
`rpt_DisagreementCount` across every affected report. Still no UI: no user list, no
block button.

---

## Settled

**GetMyReports returning 403 to Staff is deliberate.** Yesterday's version gave Staff a
branch of its own here. It was replaced on purpose — Staff use `GET api/Reports`, which
already returns their baladiye's reports, so there's no reason to check for Staff in
this endpoint. Left as it is.

---

## Bugs that were always there

Present and wrong, roughly in the order worth fixing.

| # | Where | Problem |
|---|---|---|
| 1 | `Services/LatePenaltyService.cs` | `ApplyPenalties()` runs *before* the 24h delay, so a full penalty pass fires on every app start. Five debug restarts = five points off per overdue report. No `try/catch` either — since .NET 6 an unhandled exception in a `BackgroundService` stops the whole host, so a DB that isn't up yet at startup takes the API down. |
| 2 | `ReportsAdminController.UpdateReportStatus` | `SET rpt_ResolvedPhotoUrl = @ResolvedPhotoUrl` is unconditional, and the field is only *required* when the status is Resolved. Move a resolved report back to In Progress and the after-photo URL is NULLed permanently; the file is orphaned in `wwwroot/uploads`. |
| 3 | `ReportForm/CreateReportForm.jsx` | The API returns `existingReportId` so React can send the resident to the existing report to vote on its priority. The client shows a sentence and discards the id — `useNavigate` isn't even imported. The welcome page advertises this behaviour as a feature. |
| 4 | `ReportDetail/ReportDetail.jsx` | `ReportNavbar` accepts `backLabel` / `backTo` and no caller passes them. `ReportDetail` hand-rolls its own nav with no Logout and no Dashboard. |
| 5 | `services/api.js` | The axios instance with the JWT interceptor is imported by nobody; every component uses raw `fetch` with a hardcoded URL. No central 401 handling, so an expired token shows a 401 string forever instead of redirecting to login. |
| 6 | `ReportsAdminController` comments | Several refer to `PUT {id}/accept` as though it exists. It does not exist anywhere in the project. `Models/AcceptReportRequest.cs` is dead too — superseded by `MunicipalityRequest`, never deleted. |

---

## Reported but not real

Two independent review agents ran over the code. These came back and did **not**
survive checking — artefacts of an incomplete copy, not problems on disk.

- **"Four CSS files and main.jsx are missing, the app will not build."** `Report.css`,
  `Dashboard.css`, `Register.css`, `Welcome.css`, `theme.css` and `main.jsx` are all
  present.
- **"The boundary seeder does not exist."** `CivicFix.Seeder` is there. The related
  complaint about migrations creating unprefixed table names is worth a look
  eventually — a clean `dotnet ef database update` would not reproduce the schema —
  but it breaks nothing today.

---

## Still worth doing

**Run git.** This log covers what could be proven by comparing yesterday's copies
against today's. It cannot catch work that is still reachable but quietly less
complete — a validation deleted, a condition simplified, a field dropped from a query.
The daily limit only surfaced because that file happened to have been read the day
before. `git log --oneline -10` is the only complete answer.

**Move the secrets.** `appsettings.json` holds the Gmail app password and the JWT
signing key in plain text, inside a git repository. The email address is fine to
publish; the password beside it is not. Use user-secrets or an environment variable,
and revoke that app password if the repo has been pushed anywhere public.
