namespace CivicFix.Api.Models
{
    // The body for any request that says "this baladiye" — currently
    // PUT api/Reports/{id}/assign-handler  and  PUT api/Reports/{id}/move.
    //
    // RENAMED from AcceptReportRequest. That name came from an endpoint,
    // PUT {id}/accept, that has since been deleted: a Staff member claiming a
    // report for their own baladiye. Its job was taken over by AssignHandler
    // (the Admin chooses) and UpdateReportStatus (which works out the handler
    // by itself when a report is resolved), so nothing called it any more.
    //
    // The JSON is unchanged — still { "MunicipalityId": 248 } — so React needed
    // no edit. Only the C# type name is different.
    public class MunicipalityRequest
    {
        public int MunicipalityId { get; set; }
    }
}
