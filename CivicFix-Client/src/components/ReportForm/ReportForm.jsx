import { useState, useEffect } from "react";
import Navbar from "../layout/Navbar";
import StaffBaladiyeBadge from "./StaffBaladiyeBadge";
import ReportTabs from "./ReportTabs";
import StatusFilterBar from "./StatusFilterBar";
import CreateReportForm from "./CreateReportForm";
import ReportCard from "./ReportCard";

import "../../styles/Report.css";

function ReportForm() {
  const role = localStorage.getItem("usr_Role"); 

  const [activeTab, setActiveTab] = useState("all"); 
  const [statusFilter, setStatusFilter] = useState("All"); 
  const [categories, setCategories] = useState([]); 
  const [showForm, setShowForm] = useState(false); 


  const [reports, setReports] = useState([]);
  const [listLoading, setListLoading] = useState(true);
  const [listError, setListError] = useState("");


  const fetchReports = async (tab) => {
    setListLoading(true);
    setListError("");
    try {
      const token = localStorage.getItem("token");


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
      
    }
  };

  useEffect(() => {
    fetchReports(activeTab);
}, [activeTab]);

  useEffect(()=>{
    fetchCategories();
  },[])

  const visibleReports =
    statusFilter === "All"
      ? reports
      : reports.filter((r) => r.rpt_Status === statusFilter);

  return (
    <div className="report-page">
            <Navbar />

      <div className="report-container">

        
        <div className="report-header">
          <div>
            <h1 className="report-header__title">Community Reports</h1>
            <p className="report-header__sub">بلاغات منطقتك — latest issues reported</p>

           
            {role === "Staff" && <StaffBaladiyeBadge />}
          </div>

          <button className="btn-toggle-form" onClick={() => setShowForm(!showForm)}>
            {showForm ? "✕ Close" : "🚨 Report a Problem"}
          </button>
        </div>

        <ReportTabs role={role} activeTab={activeTab} setActiveTab={setActiveTab} />

       
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
              fetchReports(activeTab);
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
