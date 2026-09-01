# CivicFix — reading order

A route through 63 files, ordered so nothing you read raises a question an earlier
file hasn't already answered. Every path, line number and line count below was
verified against the files on disk.

Companion to `CIVICFIX-WALKS.md` (individual request traces) and
`CIVICFIX-RECOVERY.md` (known bugs and lost work).

---

## If you only have one evening

`Program.cs` → `ReportsController.CreateReport` → `ReportsController.GetAllReports`
→ `ReportsAdminController.UpdateReportStatus`

Those four cover the wiring, the routing, the roles and the points. That's what
anyone will actually ask you about.

---

## Foundation — read these six first

About 45 minutes. Everything after assumes you have them in your head.

| File | Why it earns this position |
|---|---|
| `CivicFix.Api/Program.cs` | This project's `settings.py` + `urls.py` + `wsgi.py` on one page |
| `CivicFix.Api/CivicFix.Api.csproj` | The `requirements.txt`. Only place that explains why **both** Dapper and EF Core are installed |
| `Models/Report.cs`, `Municipality.cs`, `User.cs`, `ReportAssignment.cs` | The four nouns the system is about — 68 lines total |
| `Data/AppDbContext.cs` | The table map, and the `Restrict` rules that dictate the manual delete order you meet three times later |
| `CivicFix-Client/src/App.jsx` | 28 lines. The browser's `urls.py` |
| `src/services/apiHelpers.js` | 77 lines. The shared vocabulary — ten components import from it |

**Inside `Program.cs`, four things in this order:**

1. Lines 36–39, `PropertyNamingPolicy = null` + the comment above it. Best story in the project to tell in a presentation.
2. Lines 41–44, `AddScoped<SqlConnection>` — this is `_connection` in every controller. Dapper is `cursor.execute`, not the Django ORM.
3. Lines 47–55, `AddDbContext` — registered, but migration-time only. Nothing at runtime touches it.
4. Lines 129–139, middleware order. CORS used to sit *after* `MapControllers` and was never reached.

---

## Stage 1 — Identity: how the server knows who you are

*Every controller method below starts with `User.FindFirst("Id")`, meaningless until
you've watched the claim being created.* (~1h)

| File | What's new | Look at |
|---|---|---|
| `UsersController.cs` lines 29–131 only | JWT minting: BCrypt, three claims, 12h expiry | `Register` line 46 — role hard-coded `"Resident"`. `Login` 107–112, the claims array. 99–103, the blocked check and why it's *after* the password check |
| `auth/LoginForm.jsx` | Where the token lands | Lines 33–39: four `localStorage.setItem` calls. That's the whole session model |
| `auth/RegisterForm.jsx` | A variation, nothing structural | Lines 41–48 only; skim the JSX |
| `components/ReportNavbar.jsx` | Logout | Lines 23–32: removing the four keys *is* logging out |

**Django note:** the token is your session cookie, except nothing attaches it
automatically. Every `fetch` sets `Authorization: Bearer` by hand.

---

## Stage 2 — One complete round trip, nothing in the way

*Same shape as Stage 1 with auth removed — the cleanest look at Dapper → `Ok(...)` →
React state.* (~30m)

| File | What's new | Look at |
|---|---|---|
| `MunicipalitiesController.cs` | Smallest endpoint; introduces `mun_TotalPoints` | Line 27 and its comment on `QueryAsync<dynamic>` |
| `components/Dashboard.jsx` | `useState` + `useEffect` + loading/error/data | Lines 38–41: `originalRank` stamped *before* filtering, so ranks don't renumber on search |
| `CategoriesController.cs` | The only endpoint with no `[Authorize]` | 26 lines, one glance |

---

## Stage 3 — 🫀 The spatial core

*Needs the token from Stage 1 and writes the assignment rows the foundation
described. The idea the whole project exists for.* (~3h)

| File | What's new | Look at |
|---|---|---|
| `CivicFix.Seeder/Program.cs` | **Where the polygons come from.** Read before the spatial query or `STContains` is magic | Line 70 `geography::STGeomFromText(@WKT, 4326)` — 4326 is plain GPS. `BuildPolygonWkt` 87–97 closes the ring. Line 37: areas with no municipality get an `M-` prefix |
| `ReportsController.CreateReport` 35–270 | The spatial query, Staff/Resident split, daily limit, duplicate check, assignment fan-out | Five passes, below |
| `UploadsController.cs` | `IFormFile`, extension whitelist, GUID renaming | Lines 56–64: the original filename is discarded because `..\..\appsettings.json` would be path traversal |
| `ReportForm/CreateReportForm.jsx` | Upload-then-create ordering; role-conditional fields | Lines 85–93: **both photos upload first**, then the report — the row stores a URL, so the file must exist. Line 125: `existingReportId` received and discarded (live bug) |
| `ReportForm/MapPicker.jsx` | Leaflet; `useRef` as "survives re-render without causing one" | Cleanup return 109–113 — without it, reopening throws "Map container is already initialized" |

### CreateReport in five passes

1. **40–42** — who is asking. From the token, never the body.
2. **46–115, Staff branch** — may only report inside their own baladiye. `STContains` is inside-or-not, `STDistance` is metres to the edge, `STArea()` is a sanity check. Lines 98–100 twice: a polygon over 20,000 km² is wound backwards and covers the planet minus the town.
3. **124–136, Resident branch** — **the sentence the project is built on.** One query returns *every* baladiye whose polygon contains the point or sits within 100 m. Usually one row; on a border, two or more. That plural result creates the entire shared-report problem.
4. **146–220** — daily limit (counted on `currentUserId`, not `request.ReporterId`) and duplicate check. Asymmetry at 199–220: Resident gets `200 OK` with `existingReportId`, Staff get `400`.
5. **247–267** — the fan-out, and the most consequential line in the file:

```csharp
bool ownedOnCreate = reporterRole == "Staff" || municipalities.Count() == 1;
```

---

## Stage 4 — Role-based filtering: one statement, three answers

*These WHERE clauses filter the rows Stage 3 created.* (~2h 15m)

| File | What's new | Look at |
|---|---|---|
| `GetAllReports` 273–348, `GetMyReports` 351–423 | The three-piece SQL stitch; `STRING_AGG` + `GROUP BY`; `EXISTS` as a filter | Lines 312–324 — the whole role model in one variable. Staff get `EXISTS` **plus** `COUNT(*) = 1`, so an undecided shared report is invisible to them |
| `ReportForm/ReportForm.jsx` | **The composition hub** — how six components fit together | Lines 99–102: the status filter runs in the browser on already-fetched data. **Ignore the filename** — this is the list page, not a form |
| `ReportForm/ReportTabs.jsx` | Why Staff see no tabs | Lines 18–20: for Staff both tabs returned identical lists |
| `ReportForm/StaffBaladiyeBadge.jsx` | A component fetching its own data; `LEFT JOIN` | Read with `UsersController.GetMe` 223–251 |
| `ReportForm/ReportCard.jsx` | Three admin actions on one card; `e.stopPropagation()` | Lines 246–248: handles **two response shapes** — `Candidates` from `/shared`, or an `AssignedMunicipalities` string elsewhere |

---

## Stage 5 — One report, in full

*`GetReportById` aggregates everything created so far.* (~1h 35m)

| File | What's new | Look at |
|---|---|---|
| `GetReportById` 425–583 | **Seven queries into one response**; reading `.Lat`/`.Long` back out of a geography column | 435–454: the Staff guard twice — wrong baladiye → 403, undecided shared → 403. The commented-out JSON at 586–622 is the shape React consumes |
| `ReportDetail/ReportDetail.jsx` | Parent/child: one fetch, props down, callbacks up | Lines 339–355: `key={report.rpt_Status}` — `useState` only reads its initial value on mount, so changing `key` forces a remount with fresh state |
| `ReportDetail/ReportComments.jsx` | The simplest child | Line 30: `UserId` sent and ignored. The JWT wins |

---

## Stage 6 — The accountability loop

*`GetReportById` already handed you `MyPriorityVote` and `MyAgreement`. Must come
before Stage 7 — this is the first of two doors into the points economy.* (~1h 15m)

| File | What's new | Look at |
|---|---|---|
| `ReportsFeedbackController.cs` | Two kinds of participation, deliberately asymmetric; first place points are awarded | `AgreeOnReport`: only on **Staff** reports, one shot. 129–156: three agreements = +10, guarded by `rpa_Points == 0`. `VoteOnPriority`: only on **Resident** reports, and changeable |
| `ReportDetail/ReportPriorityVote.jsx` | How the asymmetry looks in the UI | Line 135: `!== null && !== undefined` rather than truthiness, because `false` is a real answer |

---

## Stage 7 — 🫀 The points economy and admin power

*Awards the same +10 through a different door. Read `ReportsAdminController.cs` in
**this** order, not file order.* (~2h 40m)

| Method | What's new | Look at |
|---|---|---|
| `UpdateReportStatus` 139–307 | Where points are **earned**; working out which baladiye did the work | 174–192: the Staff ownership check — Admin skips this block entirely. **230–304 are the heart of the heart** |
| `GetSharedReports` 21–136 | **Two queries stitched in C#**, not SQL | 40–44: `>= 2` assignment rows *is* the definition of shared. Read `Models/SharedReportDto.cs` alongside |
| `AssignHandler` 311–409 | Your first **transaction**, and what it protects | 337–341 first, then the comment above. Without that guard an unassigned id deletes *all* rows while the update matches none — report unrecoverable, transaction commits, response says it worked |
| `MoveReport` 413–522 | Points **clawback**; an override that ignores the spatial result | `AssignHandler` picks among spatial candidates; this picks *any* baladiye in Lebanon |
| `DeleteReport` 526–591 | Delete-children-first cascade, driven by `Restrict` | 549–567: read assignments *before* deleting, so you still know what to claw back |

---

## Stage 8 — The parts that run without a request

*Each is a variation on a pattern you know; the penalty service only makes sense
once you know how points are earned.* (~1h 20m)

| File | What's new | Look at |
|---|---|---|
| `Services/LatePenaltyService.cs` | The **minus** side; a `BackgroundService` (roughly a Celery beat task, in-process) | 36–48: one `UPDATE...FROM` — −1 point per unresolved report older than 7 days. Then 17–25 and the known bug |
| `UsersController.BlockUser` 268–356 | The `DeleteReport` cascade across all a user's reports, with bulk `IN @Ids` | Dapper expands a C# list into SQL `IN (...)`. Note there's no unblock endpoint |
| `EmailSender.cs` + forgot/reset | One-time token with expiry, marked used after redemption | GUID token, 1h expiry. Reset checks four things: exists, not expired, not used, then hash and mark used |

---

## The true heart

**`ReportsController.cs` (622 lines)** — the reason the project exists. If you can
explain lines 124–136 and line 255 fluently, you can explain CivicFix.

**`ReportsAdminController.cs` (593 lines)** — where the points economy lives and
every hard decision is made. Also the most instructive near-miss, at 337–341.

**`Program.cs` (143 lines)** — small, but every cross-cutting decision is visible on
one page. Know it and you can answer "how does it all fit together?" without opening
anything else.

---

## Concepts that recur everywhere

**1. Spatial containment routing.** A baladiye is a polygon, not a dropdown choice.
Send a lat/long and SQL Server asks every polygon "do you contain this point, or come
within 100 m?" **A plural answer is normal** — that's what creates the shared-report
problem. → `ReportsController.cs` 124–136; read `Seeder/Program.cs:70` first.

**2. The assignment / handler model.** No `rpt_MunicipalityId` on a report. Instead
one `tbl_ReportAssignments` row per candidate, and at most one with
`rpa_IsHandler = 1`. One candidate → handler immediately. Two or more → nobody owns
it. Every list query `INNER JOIN`s this table, which is why zero assignment rows makes
a report invisible. → `ReportsController.cs:247-267` and `ReportsAdminController.cs:311-409`.

**3. Role-based query filtering.** `baseSql + whereClause + groupOrderSql`, and only
the middle changes by role. Filtering happens in the database — never in C#, never in
React. → `GetAllReports` 291–344.

**4. The points economy.** `mun_TotalPoints` moves four ways: +10 on Resolved, +10 on
three agreements (two doors, same payout), −1 daily per report overdue past 7 days,
and clawback when work stops counting. The guard is `rpa_Points != 0` — points are
per-assignment, so zero means "not paid yet". → `UpdateReportStatus` 230–304.

**5. JWT claims as the only identity.** Three facts baked into a signed 12-hour
token. The body is never trusted for identity, even though several DTOs still carry a
`UserId`. React still sends them; the server ignores every one — that's the fix, not
sloppiness. → `Login` 107–130 → `Program.cs` 59–76 → any controller's first three lines.

**6. Restrict FKs → hand-written delete order.** No cascade anywhere. Six deletes by
hand in a fixed order, in a transaction, points clawed back first. →
`AppDbContext.cs:32-73` then `DeleteReport:549-578`.

**7. Why the JSON isn't camelCase.** ASP.NET renames C# properties on the way out,
but Dapper's `dynamic` rows are dictionaries and the rule doesn't apply to dictionary
keys — so lists came through as `rpt_Id` while class-based responses got renamed. Two
conventions in one API. `PropertyNamingPolicy = null` turns it off. →
`Program.cs` 14–39. The most presentable debugging story in the project.

---

## Skip or skim

**Dead code — confirm once, then ignore:**

- `src/services/api.js` — axios instance with a JWT interceptor. Imported only by `authService.js`, which is imported by nobody.
- `src/services/authService.js` — `loginUser`/`registerUser` are correct and uncalled. **Its comments are worth 5 minutes** — they document real bugs that were fixed.
- `src/App.css` — Vite starter leftovers, imported by nobody.
- `Models/AcceptReportRequest.cs` — superseded by `MunicipalityRequest.cs`. Read that file's 10-line header instead.

**Boilerplate, two minutes for all four:** `index.html`, `src/main.jsx`,
`vite.config.js`, `package.json`.

**Repetitive:** the eight `Models/*Request.cs` files (read `CreateReportRequest` and
`UpdateStatusRequest` properly, skim the rest); the child-table models; the two
password screens.

**Read last, for pleasure:** `WelcomePage.jsx` (527 lines) teaches almost nothing
about the system. But read its `FEATURES` array before presenting — those six tiles
are an accurate plain-English summary of what the backend genuinely does.

---

## Files whose names mislead

| File | You'd assume | What it actually is |
|---|---|---|
| `ReportsController.cs` | All report endpoints | Only create/list/detail. **Four controllers share** `[Route("api/Reports")]`; lines 19–24 explain why |
| `ReportsAdminController.cs` | Admin-only | `UpdateReportStatus` is `Roles = "Staff,Admin"` |
| `ReportsFeedbackController.cs` | A URL with "feedback" | The class name never appears in a URL |
| `ReportForm/ReportForm.jsx` | The submission form | **The list page.** The form is `CreateReportForm.jsx` |
| `ReportForm/` folder | Form components | Mostly isn't: `ReportCard`, `ReportTabs`, `StatusFilterBar`, `StaffBaladiyeBadge`, `MapPicker` |
| `Services/` | A Django-style service layer | Two DI helpers. **All business logic is in the controllers** |
| `Models/User.cs` | One class | Two — `User` and `LoginRequest` |
| `Data/AppDbContext.cs` | The runtime data layer | Migration-time only |

---

## Where CIVICFIX-WALKS.md is now wrong

The "five steps every endpoint follows" box at the bottom is still exactly right.
These claims are not — the front end was split into components after it was written.

| It says | Reality |
|---|---|
| `src/components/ReportForm,.jsx` — "189 lines" | No such file (stray comma). `components/ReportForm/ReportForm.jsx`, 183 lines |
| Walk 10: `votePriority`, `submitAgreement`, `postComment` in `ReportDetail.jsx` | First two are in `ReportPriorityVote.jsx`; `postComment` in `ReportComments.jsx` |
| Walk 11: `ReportDetail.jsx` `saveStatus` | Moved to `ReportStatusPanel.jsx`. The `ReportCard.jsx` one is still right — two different functions share the name |
| Walk 13: `ReportDetail.jsx` `moveReport` | Moved to `MoveReportPanel.jsx` |
| "Three endpoints nothing calls: `location-check`, `forgot-password`, `reset-password`" | All three wrong. `location-check` no longer exists; the other two are called by their screens and linked from login |
| "When you finish 13 you have seen every file that talks to the API" | Four callers sit outside the 13: `blockReporter`, `MoveReportPanel`, and the two password screens |
| "`Models/` + `AppDbContext.cs` — the table shapes" | Half true. `AppDbContext` has **no `DbSet`** for `PriorityVote` or `ReportAgreement`, yet both tables are queried constantly |

---

## Pacing

| Session | Stages | Time |
|---|---|---|
| 1 | Foundation + Stage 1 | ~1h 45m |
| 2 | Stages 2 + 3 | ~3h 20m |
| 3 | Stages 4 + 5 | ~3h 40m |
| 4 | Stages 6 + 7 | ~4h |
| 5 | Stage 8 + skim list | ~1h 45m |
