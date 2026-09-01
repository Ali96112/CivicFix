// The tab row: which SET of reports to fetch.
//
// Split out of ReportForm.jsx. The tabs choose the endpoint; the status filter
// below them narrows whatever comes back. Two different jobs, two components.

function ReportTabs({ role, activeTab, setActiveTab }) {
  // The whole row is hidden for STAFF.
  //
  // WHY: for a Staff user the two tabs called the same thing twice.
  // "Baladiye Reports" hits GET api/Reports, which filters to their baladiye,
  // and "My Baladiye" hit GET api/Reports/mine, which filters to their baladiye
  // as well — two buttons, identical lists. (This is the same redundancy the
  // Admin tab had before it became the Shared Reports screen.)
  //
  // With only one meaningful tab left, the row itself is noise, so Staff go
  // straight to the status filters. Which baladiye they are looking at is
  // already stated in the header line above.
  if (role === "Staff") {
    return null;
  }

  // Resident and Admin keep both tabs: for a Resident the second one is
  // genuinely different (only the reports THEY submitted), and for an Admin
  // it is the Shared Reports screen.
  const secondTab = role === "Admin" ? "shared" : "mine";

  return (
    <div className="report-tabs">
      <button
        className={`report-tab ${activeTab === "all" ? "report-tab--active" : ""}`}
        onClick={() => setActiveTab("all")}
      >
        All Reports
      </button>

      <button
        className={`report-tab ${activeTab === secondTab ? "report-tab--active" : ""}`}
        onClick={() => setActiveTab(secondTab)}
      >
        {role === "Admin" ? "🔀 Shared Reports" : "My Reports"}
      </button>
    </div>
  );
}

export default ReportTabs;
