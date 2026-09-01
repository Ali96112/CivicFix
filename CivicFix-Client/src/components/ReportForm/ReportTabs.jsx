function ReportTabs({ role, activeTab, setActiveTab }) {
 
  if (role === "Staff") {
    return null;
  }

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
