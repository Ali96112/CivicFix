namespace CivicFix.Api.Models
{
    // ADDED — these two classes are what GET api/Reports/shared sends back to React.
    //
    // WHY A REAL CLASS INSTEAD OF `new { ... }`:
    // the rest of this project returns anonymous objects, which is fine for simple
    // shapes. Here the shape is nested (a report that carries a list of baladiyat)
    // and it is built from Dapper `dynamic` rows. Mixing dynamic values into an
    // anonymous type makes the compiler infer `dynamic` properties, which are only
    // checked at runtime and are awkward for the JSON serializer.
    // Plain classes with real types keep everything compile-checked and serialize
    // predictably.

    // one baladiye the Admin can push a shared report to — this is one button in the UI
    public class HandlerCandidate
    {
        public int mun_Id { get; set; }             // sent back when the admin clicks
        public string mun_Name { get; set; } = "";  // the text on the button
        public bool IsHandler { get; set; }         // true = this baladiye currently owns the report
        public DateTime? AcceptedAt { get; set; }   // when it was chosen, null if never
    }

    // one report that sits on the border between two or more baladiyat
    public class SharedReportDto
    {
        public int rpt_Id { get; set; }
        public string? rpt_Title { get; set; }
        public string? rpt_Description { get; set; }
        public string? rpt_Status { get; set; }
        public DateTime rpt_CreatedAt { get; set; }
        public string? rpt_ReportedPhotoUrl { get; set; }
        public string? rpt_Priority { get; set; }   // nullable — staff reports have no priority
        public int rpt_AgreementCount { get; set; }
        public string? CategoryName { get; set; }

        // every baladiye this report was assigned to — the admin picks one of these
        public List<HandlerCandidate> Candidates { get; set; } = new List<HandlerCandidate>();

        // true when none of the candidates is the handler yet, i.e. nobody owns the
        // problem and the admin still has to decide. React uses this for the warning text.
        public bool NeedsDecision { get; set; }
    }
}
