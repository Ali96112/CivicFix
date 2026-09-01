# CivicFix — the 13 walks

Follow one request at a time, front to back.
When you finish 13 you have seen every file that talks to the API.

---

## Read first (10 minutes, no API calls)

| File | Why |
|---|---|
| `CivicFix.Api/Program.cs` | how the app boots, and the middleware order |
| `src/App.jsx` | how a URL picks a component |
| `src/services/apiHelpers.js` | 77 lines — walks 7, 8, 11, 12, 13 all use it |
| `src/components/ReportForm,.jsx` | 189 lines — how the six pieces fit together |

---

## The walks

| # | Walk | Front | → Back |
|---|---|---|---|
| 1 | Register | `RegisterForm.jsx` `handleSubmit` | `UsersController.Register` |
| 2 | Login | `LoginForm.jsx` `handleSubmit` + `api.js` | `UsersController.Login` |
| 3 | Leaderboard | `Dashboard.jsx` `fetchMunicipalities` | `MunicipalitiesController.GetDashboard` |
| 4 | Categories | `ReportForm.jsx` `fetchCategories` | `CategoriesController.GetAllCategories` |
| 5 | Staff baladiye | `StaffBaladiyeBadge.jsx` `fetchMe` | `UsersController.GetMe` |
| 6 | **Reports list** | `ReportForm.jsx` `fetchReports` | `ReportsController.GetAllReports` + `GetMyReports` |
| 7 | Photo upload | `services/apiHelpers.js` `uploadPhoto` | `UploadsController.UploadPhoto` |
| 8 | **Create report** | `CreateReportForm.jsx` `handleSubmit` | `ReportsController.CreateReport` |
| 9 | Open a report | `ReportDetail.jsx` `fetchReport` | `ReportsController.GetReportById` |
| 10 | Vote / agree / comment | `ReportDetail.jsx` `votePriority`, `submitAgreement`, `postComment` | `ReportsFeedbackController` (all 3) |
| 11 | Change status | `ReportCard.jsx` `saveStatus` · `ReportDetail.jsx` `saveStatus` | `ReportsAdminController.UpdateReportStatus` |
| 12 | Shared → assign | `ReportForm.jsx` `fetchReports("shared")` · `ReportCard.jsx` `assignHandler` | `GetSharedReports` + `AssignHandler` |
| 13 | Move / delete | `ReportDetail.jsx` `moveReport` · `ReportCard.jsx` `deleteReport` | `MoveReport` + `DeleteReport` |

**1–5** are small — an hour total, and they teach you the shape.
**6 and 8** are the two that matter. Spend real time there.
**9–13** go fast once 8 makes sense.

---

## What each walk teaches you

| # | New in this walk |
|---|---|
| 1 | password hashing; role forced to `"Resident"` server-side |
| 2 | the JWT — what's in it, how it's signed, where React keeps it |
| 3 | nothing new — the simplest round trip, a confidence win |
| 4 | an endpoint with no `[Authorize]` at all |
| 5 | `LEFT JOIN` and why it matters; a component that fetches its own data |
| 6 | reading the token; `whereClause` by role; `EXISTS` vs `WHERE`; `STRING_AGG` + `GROUP BY` |
| 7 | `FormData` not JSON; why no `Content-Type`; why the file is renamed to a GUID |
| 8 | the spatial query; upload-then-create order; one assignment row per baladiye |
| 9 | seven queries in one endpoint; the Staff 403 |
| 10 | the accountability loop — 3 agreements = +10 points; why one vote is changeable and one isn't |
| 11 | where points are awarded; working out *which* baladiye resolved it |
| 12 | two queries stitched in C#; a transaction, and what it protects against |
| 13 | children deleted before the parent; points given back |

---

## Not covered by the 13 (no API calls — read any time)

| File | What it is |
|---|---|
| `WelcomePage.jsx` | the landing page |
| `MapPicker.jsx` | the Leaflet map (feeds walk 8) |
| `main.jsx` | 10 lines, mounts React |
| `ReportNavbar.jsx` · `ReportTabs.jsx` · `StatusFilterBar.jsx` | small display pieces |
| `Models/` + `Data/AppDbContext.cs` | the table shapes |

Three endpoints nothing calls: `location-check` (debug tool), `forgot-password`,
`reset-password` (built, but no screen for them).

---

## The pattern

Every endpoint is the same five steps:

```
1. WHO      read Id + Role from the token — never from the request body
2. ALLOWED? [Authorize] + baladiye check           → 401 / 403
3. VALID?   is this a real status? a real baladiye? → 400
4. SQL      QueryAsync / ExecuteAsync
5. RETURN   Ok(...) / BadRequest(...) / NotFound(...)
```

Only steps 3 and 4 change between them.
