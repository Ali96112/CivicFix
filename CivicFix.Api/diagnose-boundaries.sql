-- ═══════════════════════════════════════════════════════════════════════════
-- CivicFix — why is everything being assigned to the wrong baladiye?
--
-- Run these in SQL Server Management Studio against the CivicFix database.
-- Each query answers one specific question. Run them in order and stop at
-- the first one that looks wrong.
-- ═══════════════════════════════════════════════════════════════════════════


-- ───────────────────────────────────────────────────────────────────────────
-- CHECK 1 — INVERTED POLYGONS.  *** run this one first ***
--
-- This is the most likely cause of "everything gets assigned to Beirut".
--
-- SQL Server's `geography` type is ORIENTATION SENSITIVE. A polygon's outer
-- ring must be wound counter-clockwise. If a polygon was imported wound the
-- other way, SQL Server does not complain — it silently interprets it as the
-- WHOLE EARTH MINUS that area. STContains() then returns 1 for almost every
-- point on the planet, so every report gets assigned to that baladiye.
--
-- Lebanon is about 10,450 km2 in total. A single baladiye should be anywhere
-- from a fraction of a km2 up to maybe a few hundred km2.
--
-- WHAT TO LOOK FOR: any row with an area in the hundreds of thousands or
-- millions of km2 is inverted. (The whole Earth is about 510,000,000 km2.)
-- ───────────────────────────────────────────────────────────────────────────
SELECT TOP 20
    mun_Id,
    mun_Name,
    mun_Boundary.STArea() / 1000000.0 AS AreaSquareKm,
    CASE
        WHEN mun_Boundary.STArea() / 1000000.0 > 20000
            THEN '*** INVERTED — this polygon is wound the wrong way ***'
        WHEN mun_Boundary.STArea() / 1000000.0 > 1000
            THEN 'suspiciously large, check it'
        ELSE 'looks plausible'
    END AS Verdict
FROM tbl_Municipalities
ORDER BY mun_Boundary.STArea() DESC;


-- ───────────────────────────────────────────────────────────────────────────
-- CHECK 2 — WHAT DOES MY OWN LOCATION RESOLVE TO?
--
-- Put your real coordinates in the two variables below, then run it.
-- To get them: open any report you submitted on the detail page (/report/<id>)
-- and read the "Location" line — that is the exact point stored for it.
--
-- This runs the same test CreateReport runs, and shows the distance to each
-- baladiye so you can see which ones the 100m rule is pulling in.
-- ───────────────────────────────────────────────────────────────────────────
DECLARE @Latitude  FLOAT = 33.8938;   -- <-- replace with your latitude
DECLARE @Longitude FLOAT = 35.5018;   -- <-- replace with your longitude

DECLARE @MyPoint GEOGRAPHY = geography::Point(@Latitude, @Longitude, 4326);

SELECT
    mun_Id,
    mun_Name,
    mun_Boundary.STContains(@MyPoint)      AS ContainsMyPoint,   -- 1 = inside the polygon
    mun_Boundary.STDistance(@MyPoint)      AS MetresAway,        -- 0 when inside
    mun_Boundary.STArea() / 1000000.0      AS AreaSquareKm
FROM tbl_Municipalities
WHERE mun_Boundary.STContains(@MyPoint) = 1
   OR mun_Boundary.STDistance(@MyPoint) < 5000   -- widened to 5km so you can see the neighbours
ORDER BY mun_Boundary.STDistance(@MyPoint);

-- HOW TO READ THIS:
--   * several rows with ContainsMyPoint = 1
--         → polygons overlap, or some are inverted (see Check 1)
--   * Beirut appears with ContainsMyPoint = 1 but you are not in Beirut
--         → Beirut's polygon is wrong: either inverted, or the imported
--           coordinates were lat/long swapped when the boundaries were seeded
--   * no rows at all
--         → your point is outside every polygon; the seed data does not cover
--           your area


-- ───────────────────────────────────────────────────────────────────────────
-- CHECK 3 — ARE THE STORED BOUNDARIES IN THE RIGHT PART OF THE WORLD?
--
-- Lebanon sits at roughly latitude 33.0 to 34.7 and longitude 35.1 to 36.6.
--
-- If a polygon's CENTRE comes back with latitude around 35 and longitude
-- around 33, the coordinates were SWAPPED during the import — GeoJSON stores
-- [longitude, latitude], which is the opposite of how most people write them,
-- and it is the single most common mistake when seeding boundary data.
-- ───────────────────────────────────────────────────────────────────────────
SELECT TOP 20
    mun_Id,
    mun_Name,
    mun_Boundary.EnvelopeCenter().Lat  AS CentreLatitude,   -- expect ~33.0 to 34.7
    mun_Boundary.EnvelopeCenter().Long AS CentreLongitude,  -- expect ~35.1 to 36.6
    CASE
        WHEN mun_Boundary.EnvelopeCenter().Lat  BETWEEN 33.0 AND 34.8
         AND mun_Boundary.EnvelopeCenter().Long BETWEEN 35.0 AND 36.7
            THEN 'inside Lebanon, good'
        ELSE '*** OUTSIDE LEBANON — coordinates are probably swapped ***'
    END AS Verdict
FROM tbl_Municipalities
ORDER BY mun_Id;


-- ───────────────────────────────────────────────────────────────────────────
-- CHECK 4 — WHERE ARE THE EXISTING REPORTS, REALLY?
--
-- Shows each report's stored point next to the baladiyat it was assigned to.
-- If a report's own coordinates look right but the baladiye is wrong, the
-- problem is the boundary data, not the report.
-- ───────────────────────────────────────────────────────────────────────────
SELECT
    r.rpt_Id,
    r.rpt_Title,
    r.rpt_Location.Lat  AS ReportLatitude,
    r.rpt_Location.Long AS ReportLongitude,
    m.mun_Name          AS AssignedTo,
    a.rpa_IsHandler     AS IsHandler
FROM tbl_Reports r
INNER JOIN tbl_ReportAssignments a ON r.rpt_Id = a.rpa_ReportId
INNER JOIN tbl_Municipalities    m ON a.rpa_MunicipalityId = m.mun_Id
ORDER BY r.rpt_Id DESC;


-- ═══════════════════════════════════════════════════════════════════════════
-- THE FIX, IF CHECK 1 FOUND INVERTED POLYGONS
--
-- ReorientObject() flips a polygon's ring direction, turning "everywhere
-- except this area" back into "this area".
--
-- Only run this on the rows Check 1 flagged. Running it on a CORRECT polygon
-- will invert that one instead, so do not run it on the whole table blindly.
--
--   UPDATE tbl_Municipalities
--   SET mun_Boundary = mun_Boundary.ReorientObject()
--   WHERE mun_Id IN ( ... the ids Check 1 flagged ... );
--
-- Then run Check 1 again — the areas should now be sensible.
--
-- After fixing the boundaries, the existing reports still carry their old
-- wrong assignments. Delete those reports and submit them again, or clear
-- tbl_ReportAssignments for them and re-run the assignment by hand.
-- ═══════════════════════════════════════════════════════════════════════════
