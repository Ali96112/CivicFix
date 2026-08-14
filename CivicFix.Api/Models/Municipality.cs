using NetTopologySuite.Geometries;

namespace CivicFix.Api.Models
{
    public class Municipality
    {
        public int mun_Id { get; set; }
        public string mun_Name { get; set; }
        public Polygon mun_Boundary { get; set; }
        public int mun_TotalPoints { get; set; }
    }
}