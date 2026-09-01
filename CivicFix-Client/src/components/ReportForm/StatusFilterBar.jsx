
const STATUS_FILTERS = [
  { key: "All", label: "All" },
  { key: "Submitted", label: "🆕 Submitted" },
  { key: "In Progress", label: "🔧 In Progress" },
  { key: "Resolved", label: "✅ Finished" },
];

function StatusFilterBar({ reports, statusFilter, setStatusFilter }) {

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
