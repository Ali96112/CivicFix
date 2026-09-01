
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
    return reports.filter((r) => r.rpt_Status === statusKey).length;//That creates a new array containing only Submitted reports and then get their length
  };

  return (
    <div className="status-filters">
      {STATUS_FILTERS.map((filter) => (//create a button for each statusfilter filter just represent each button 
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
