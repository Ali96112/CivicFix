// The tab row: which SET of reports to fetch.
//
// Split out of ReportForm.jsx. The tabs choose the endpoint; the status filter
// below them narrows whatever comes back. Two different jobs, two components.

function ReportTabs({ role, activeTab, setActiveTab }) {
  // The whole row is hidden for STAFF.
  
  if (role === "Staff") {
    return null;
  }

  // Resident and Admin keep both tabs: for a Resident the second one is
  // genuinely different (only the reports THEY submitted), and for an Admin
  // it is the Shared Reports screen.
  const secondTab = role === "Admin" ? "shared" : "mine";//both have (all) tab the differnece is in second tab if shared or mine

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
