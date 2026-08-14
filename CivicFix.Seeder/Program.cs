using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Linq;
using Dapper;

var connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=CivicFixDb;Trusted_Connection=True;TrustServerCertificate=True;";
var geoJsonPath = @"C:\Users\Win11\Downloads\LebanonAreas.geojson"; // new file path

Console.WriteLine("Reading GeoJSON file...");
var json = await File.ReadAllTextAsync(geoJsonPath);
var geoJson = JObject.Parse(json);
var features = geoJson["features"] as JArray;

Console.WriteLine($"Found {features.Count} features. Inserting municipalities...");

using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();

int inserted = 0;
int skipped = 0;

foreach (var feature in features) // Looping through every area
{
    // use AreaName from new file
    var name = feature["properties"]?["AreaName"]?.ToString();
    var hasMunicipality = feature["properties"]?["HasMunicipality"]?.ToObject<int>() ?? 0;
    var district = feature["properties"]?["District"]?.ToString();

    // skip if name is empty
    if (string.IsNullOrEmpty(name))
    {
        skipped++;
        continue;
    }

    // if no municipality — prefix with M- to indicate Mokhtar/uncovered area
    if (hasMunicipality == 0)
        name = $"M-{district}";

    var geometryType = feature["geometry"]?["type"]?.ToString();
    var coordinates = feature["geometry"]?["coordinates"];

    if (coordinates == null)
    {
        skipped++;
        continue;
    }

    try
    {
        string wkt = "";

        if (geometryType == "Polygon")
        {
            wkt = BuildPolygonWkt(coordinates[0] as JArray);
        }
        else if (geometryType == "MultiPolygon")
        {
            wkt = BuildPolygonWkt(coordinates[0][0] as JArray);
        }

        if (string.IsNullOrEmpty(wkt))
        {
            skipped++;
            continue;
        }

        // updated table and column names to match tbl_ prefix
        var sql = @"
            INSERT INTO tbl_Municipalities (mun_Name, mun_Boundary, mun_TotalPoints)
            VALUES (@Name, geography::STGeomFromText(@WKT, 4326), 0)";

        await connection.ExecuteAsync(sql, new { Name = name, WKT = wkt });
        inserted++;

        if (inserted % 50 == 0)
            Console.WriteLine($"Inserted {inserted} municipalities...");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Skipped {name}: {ex.Message}");
        skipped++;
    }
}

Console.WriteLine($"Done! Inserted: {inserted}, Skipped: {skipped}");

static string BuildPolygonWkt(JArray ring)
{
    if (ring == null || ring.Count < 3) return "";

    var points = ring.Select(p => $"{p[0]} {p[1]}").ToList();

    if (points.First() != points.Last())
        points.Add(points.First());

    return $"POLYGON(({string.Join(", ", points)}))";
}
// The full story in one sentence: reads 1,432 Lebanese area features from the GeoJSON file,
// inserts real municipalities with their names, prefixes uncovered areas with M- to indicate
// Mokhtar coverage, converts each polygon's coordinates into WKT format, and inserts them 
// into SQL Server as real geography polygons with a starting score of 0