import { useState, useEffect } from "react";
import ReportNavbar from "../ReportNavbar";
import StaffBaladiyeBadge from "./StaffBaladiyeBadge";
import ReportTabs from "./ReportTabs";
import StatusFilterBar from "./StatusFilterBar";
import CreateReportForm from "./CreateReportForm";
import ReportCard from "./ReportCard";
import "../../styles/Report.css";

//   ReportNavbar         the top bar (also used by ReportDetail)
//   StaffBaladiyeBadge   "Your baladiye: Beirut — 140 pts"
//   ReportTabs           All / My Reports / Shared Reports
//   StatusFilterBar      All · Submitted · In Progress · Finished
//   CreateReportForm     the "Report a Problem" dropdown
//   ReportCard           one report, including the admin controls
//   services/apiHelpers  readBody / errorTextOf / uploadPhoto, shared with ReportDetail
function ReportForm() {
  const role = localStorage.getItem("usr_Role"); // "Resident", "Staff", or "Admin"

  const [activeTab, setActiveTab] = useState("all"); // which set of reports to fetch
  const [statusFilter, setStatusFilter] = useState("All"); // which of those to draw
  const [categories, setCategories] = useState([]); // for the create form's dropdown
  const [showForm, setShowForm] = useState(false); // is the dropdown open?

  // ── list state ──
  const [reports, setReports] = useState([]);
  const [listLoading, setListLoading] = useState(true);
  const [listError, setListError] = useState("");

  // ── fetch the reports list ──
  const fetchReports = async (tab) => {
    setListLoading(true);
    setListError("");
    try {
      const token = localStorage.getItem("token");

      // the tab decides the endpoint:
      //   "all"    → every report the role is allowed to see
      //   "shared" → ADMIN ONLY: reports sitting on the border between 2+ baladiyat
      //   "mine"   → my own reports (Resident) / my baladiye's reports (Staff)
      const url =
        tab === "shared"
          ? "http://localhost:5140/api/Reports/shared"
          : tab === "mine"
            ? "http://localhost:5140/api/Reports/mine"
            : "http://localhost:5140/api/Reports";//if tab=all

      const response = await fetch(url, {
        headers: { Authorization: `Bearer ${token}` },
      });

      if (response.ok) {
        const data = await response.json();
        setReports(data.slice(0, 20));
      } else {
        // show the server's actual answer, not a generic sentence — the API
        // explains itself (a plain sentence, or an ASP.NET problem-details JSON)
        const rawBody = await response.text();
        console.error("Reports request failed:", response.status, url, rawBody);

        let serverMessage = rawBody;
        try {
          const parsed = JSON.parse(rawBody);
          serverMessage =
            parsed.title ||
            parsed.message ||
            (parsed.errors ? JSON.stringify(parsed.errors) : rawBody);
        } catch {
          // it was a plain sentence, keep it as-is
        }

        setListError(`Failed to load reports (${response.status}): ${serverMessage}`);
      }
    } catch (err) {
      setListError("Could not connect to server.");
    } finally {
      setListLoading(false);
    }
  };

  const fetchCategories = async () => {
    try {
      const response = await fetch("http://localhost:5140/api/Categories");
      if (response.ok) {
        setCategories(await response.json());
      }
    } catch (err) {
      // ignore
    }
  };

  useEffect(() => {
    fetchReports(activeTab);
    fetchCategories();
  }, [activeTab]); // re-run whenever activeTab changes

  // the status filter runs on the reports ALREADY fetched, so clicking is
  // instant — no extra request, no loading spinner
  const visibleReports =
    statusFilter === "All"
      ? reports
      : reports.filter((r) => r.rpt_Status === statusFilter);

  return (
    <div className="report-page">
      <ReportNavbar />

      <div className="report-container">

        {/* ── Header + Report button ── */}
        <div className="report-header">
          <div>
            <h1 className="report-header__title">Community Reports</h1>
            <p className="report-header__sub">بلاغات منطقتك — latest issues reported</p>

            {/* only Staff have a baladiye to show */}
            {role === "Staff" && <StaffBaladiyeBadge />}
          </div>

          <button className="btn-toggle-form" onClick={() => setShowForm(!showForm)}>
            {showForm ? "✕ Close" : "🚨 Report a Problem"}
          </button>
        </div>

        <ReportTabs role={role} activeTab={activeTab} setActiveTab={setActiveTab} />

        {/* hidden on the admin's Shared Reports tab, because that tab is about
            deciding which baladiye handles a report, not about its status */}
        {activeTab !== "shared" && (
          <StatusFilterBar
            reports={reports}
            statusFilter={statusFilter}
            setStatusFilter={setStatusFilter}
          />
        )}

        {showForm && (
          <CreateReportForm
            role={role}
            categories={categories}
            onCreated={() => {
              setShowForm(false);
              fetchReports(activeTab);//reload reports
            }}
          />
        )}

        {/* ── Reports list ── */}
        <div className="report-list">

          {listLoading ? (
            <p className="report-status">Loading reports...</p>
          ) : listError ? (
            <p className="report-status report-status--error">{listError}</p>
          ) : visibleReports.length === 0 ? (
            // the message has to tell the difference between "there are no
            // reports at all" and "there are reports, but none in the status you
            // filtered to" — otherwise clicking a filter looks like a bug
            <p className="report-status">
              {activeTab === "shared"
                ? "No shared reports — nothing is stuck between two baladiyat right now."
                : reports.length > 0
                  ? `No reports with status "${statusFilter}". Try another filter.`
                  : "No reports yet. Be the first to report!"}
            </p>
          ) : null}

          {visibleReports.map((rep) => (
            <ReportCard
              key={rep.rpt_Id}
              rep={rep}
              role={role}
              onChanged={() => fetchReports(activeTab)}
            />
          ))}

        </div>
      </div>
    </div>
  );
}

export default ReportForm;
