using NetTopologySuite.Geometries;

namespace CivicFix.Api.Models
{
    public class Report
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }

        public string ReportedPhotoUrl { get; set; }  // photo uploaded when reporting
        public string? ResolvedPhotoUrl { get; set; } // photo uploaded when resolved (optional until resolved)

        public Point Location { get; set; } // GPS point captured from resident's device

        public int ReporterId { get; set; }
        public User Reporter { get; set; }

        public int MunicipalityId { get; set; }
        public Municipality Municipality { get; set; }

        public int CategoryId { get; set; }
        public Category Category { get; set; }
        public int? SecondaryMunicipalityId { get; set; }
        public Municipality? SecondaryMunicipality { get; set; }

    }
}