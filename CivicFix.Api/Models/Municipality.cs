using NetTopologySuite.Geometries;

namespace CivicFix.Api.Models
{
    public class Municipality
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public Polygon Boundary { get; set; }
        public int Points { get; set; } // earned when reports are resolved
    }
}