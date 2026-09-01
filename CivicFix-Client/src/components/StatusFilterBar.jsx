// The status filter row: All · Submitted · In Progress · Finished, with counts.
//
// Split out of ReportForm.jsx.
//
// Sits under the tabs: the TABS decide WHICH reports are fetched (all / mine /
// shared), and these BUTTONS narrow that list down by status. The filtering
// happens in the browser on the reports already loaded, so clicking is instant —
// no extra request to the API. The parent does the actual filtering; this
// component only draws the buttons and reports which one was pressed.

// `key` is compared against rpt_Status, so these strings must match the database
// exactly; `label` is only what the user reads.
const STATUS_FILTERS = [
  { key: "All", label: "All" },
  { key: "Submitted", label: "🆕 Submitted" },
  { key: "In Progress", label: "🔧 In Progress" },
  { key: "Resolved", label: "✅ Finished" },
];

function StatusFilterBar({ reports, statusFilter, setStatusFilter }) {
  // counts for the little number on each button, so you can see there are
  // e.g. 3 unresolved reports without clicking through
  const countForStatus = (statusKey) => {
    if (statusKey === "All") {
      return reports.length;
    }
    return reports.filter((r) => r.rpt_Status === statusKey).length;
  };

  return (
    <div className="status-filters">
      {STATUS_FILTERS.map((filter) => (
        <button
          key={filter.key}
          type="button"
          className={`status-filter ${
            statusFilter === filter.key ? "status-filter--active" : ""
          }`}
          onClick={() => setStatusFilter(filter.key)}
        >
          {filter.label}
          <span className="status-filter__count">{countForStatus(filter.key)}</span>
        </button>
      ))}
    </div>
  );
}

export default StatusFilterBar;
