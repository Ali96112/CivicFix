# CivicFix

A municipal issue-reporting portal for Lebanon. Residents report a problem — a broken streetlight, a pothole, uncollected rubbish — and the report is routed automatically to whichever *baladiye* (municipality) covers the spot it happened, then tracked publicly until it is resolved.

Built as a full-stack capstone project: ASP.NET Core Web API + React.

---

## The problem

Reporting a municipal problem in Lebanon usually means knowing who to call and having someone answer. Most people don't, and most don't. Reports vanish, and there is no record of whether anything was ever done.

CivicFix targets two groups:

- **Residents**, who need somewhere to put a problem without knowing which office owns it.
- **Municipality staff**, who need a single queue of what is actually theirs.

What makes it different from a phone call or a Facebook post is that every report is **attributed and scored**. Each baladiye earns points when it resolves reports, and those scores are on a public leaderboard. A municipality that ignores its queue is visibly ignoring it.

## How routing works

Every baladiye's territory is stored as a real polygon (SQL Server `geography`, via NetTopologySuite). A report is stored as a point. The report belongs to whichever polygon **contains** that point — not the nearest town centre.

- Point falls inside **exactly one** baladiye → that baladiye owns the report immediately, no accept step.
- Point falls inside **two or more** (a border case) → the report stays unowned and appears on the Admin's *Shared Reports* screen, where an admin picks which baladiye handles it.

## Features

**Residents**

- Register and log in; report a problem with photo, category, priority and location
- Pick the location from a map or use browser GPS
- Comment on reports, vote on how urgent a report is, and confirm that work was actually done
- Browse all reports, a public map, and the municipality leaderboard
- Limited to 3 reports per day; near-duplicate reports (same category, same baladiye, within 30 m, recently filed) are rejected

**Municipality staff**

- See and update only their own baladiye's reports
- Change status (Submitted → In Progress → Resolved) with a required proof photo on resolve
- File reports inside their own boundary

**Admins**

- See and update every report across all baladiyat
- Resolve border cases on the Shared Reports screen
- Move a report to a different baladiye, transferring its points
- Delete reports and block abusive accounts

**Scoring** — a baladiye earns 10 points per report it resolves. Points are awarded once, never after the fact, and are reversed if the report is moved or deleted. A resolved report is frozen: it cannot be reassigned or moved.

