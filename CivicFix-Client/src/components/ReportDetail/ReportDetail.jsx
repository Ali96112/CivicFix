
import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { readBody, errorTextOf, getStatusClass } from "../../services/apiHelpers";
import ReportStatusPanel from "./ReportStatusPanel";
import ReportPriorityVote from "./ReportPriorityVote";
import MoveReportPanel from "./MoveReportPanel";
import ReportComments from "./ReportComments";
import "../../styles/Report.css";

function ReportDetail() {
  // useParams reads the ":id" part out of the URL /report/7 → id = "7"
  const { id } = useParams();//when user click on report the url show then this runs
  const navigate = useNavigate();

  const role = localStorage.getItem("usr_Role");
  const canEditStatus = role === "Admin" || role === "Staff";

  const [data, setData] = useState(null); // store data from backend
  const [loading, setLoading] = useState(true);//are we currently waiting for the backend
  const [error, setError] = useState("");

  // GET api/Reports/{id} → ReportsController.GetReportById
 
  const fetchReport = async (isFirstLoad = false) => {//firstload is just to now first time opening the scrren so it show loading on
    if (isFirstLoad) {
      setLoading(true);
    }
    setError("");
    try {
      const token = localStorage.getItem("token");
      const response = await fetch(`http://localhost:5140/api/Reports/${id}`, {
        headers: { Authorization: `Bearer ${token}` },
      });

      const body = await readBody(response);

      if (response.ok) {
        setData(body);
      } else {
        setError(errorTextOf(body, "Could not load this report."));
      }
    } catch (err) {
      setError("Could not connect to server.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchReport(true); 
  }, [id]); 

  // ── the three early states: loading, failed, and loaded ──
  if (loading) {
    return <p className="report-status">Loading report...</p>;
  }

  if (error && !data) {
    return (
      <div className="detail-page">
        <p className="report-status report-status--error">{error}</p>
        <button className="btn-back" onClick={() => navigate("/report")}>
          ← Back to reports
        </button>
      </div>
    );
  }

  if (!data) {
    return null;
  }

  const report = data.Report;

  return (
    <div className="detail-page">
      {/* ── Navbar ── */}
      <nav className="report-nav">
        <div className="report-nav__brand" onClick={() => navigate("/")}>
          <div className="report-nav__logo">🏙️</div>
          <span className="report-nav__name">
            Civic<span>Fix</span>
          </span>
        </div>
        {/* the same "who is signed in" chip as the reports page, so the navbar
            does not change shape when you open a report. The name comes from
            localStorage, saved at login — no API call needed. */}
        <div className="report-nav__right">
          {localStorage.getItem("usr_FullName") && (
            <span className="report-nav__user">
              👤 {localStorage.getItem("usr_FullName")}
              <span className="report-nav__role">{role}</span>
            </span>
          )}

          <button className="report-nav__btn" onClick={() => navigate("/report")}>
            ← Back to reports
          </button>
        </div>
      </nav>

      <div className="detail-container">
        {/* any error that happened AFTER the page loaded. Each child shows its
            own errors inside its own panel now, so this is only for the reload
            that follows one of their actions. */}
        {error && <p className="report-status report-status--error">{error}</p>}

        {/* ── headline ── */}
        <div className="detail-head">
          <span className="report-item__category">{report.CategoryName}</span>
          <span className={`report-badge ${getStatusClass(report.rpt_Status)}`}>
            {report.rpt_Status}
          </span>
        </div>

        <h1 className="detail-title">{report.rpt_Title}</h1>
        <p className="detail-desc">{report.rpt_Description}</p>

        <div className="detail-photos">
          <div className="detail-photo">
            <p className="detail-photo__label">Reported / صورة المشكلة</p>
            {report.rpt_ReportedPhotoUrl ? (
              <img
                className="detail-photo__img"
                src={report.rpt_ReportedPhotoUrl}
                alt="The reported problem"
              />
            ) : (
              <p className="detail-photo__empty">No photo</p>
            )}
          </div>

          <div className="detail-photo">
            <p className="detail-photo__label">After the fix / صورة بعد الإصلاح</p>
            {report.rpt_ResolvedPhotoUrl ? (
              <img
                className="detail-photo__img"
                src={report.rpt_ResolvedPhotoUrl}
                alt="After the fix"
              />
            ) : (
              <p className="detail-photo__empty">Not resolved yet</p>
            )}
          </div>
        </div>

        {/* ── facts table ── */}
        <div className="detail-facts">
          <div className="detail-fact">
            <span className="detail-fact__key">Reported by</span>
            <span className="detail-fact__value">
              {report.ReporterName} ({report.ReporterRole})
            </span>
          </div>
          <div className="detail-fact">
            <span className="detail-fact__key">Date</span>
            <span className="detail-fact__value">
              {new Date(report.rpt_CreatedAt).toLocaleString()}
            </span>
          </div>
          <div className="detail-fact">
            <span className="detail-fact__key">Priority</span>
            <span className="detail-fact__value">{report.rpt_Priority || "Not set"}</span>
          </div>
          <div className="detail-fact">
            <span className="detail-fact__key">Location</span>
            <span className="detail-fact__value">
              {report.Latitude != null
                ? `${Number(report.Latitude).toFixed(5)}, ${Number(report.Longitude).toFixed(5)}`
                : "Unknown"}
            </span>
          </div>
          <div className="detail-fact">
            <span className="detail-fact__key">Agreements</span>
            <span className="detail-fact__value">
              👍 {report.rpt_AgreementCount || 0} &nbsp; 👎{" "}
              {report.rpt_DisagreementCount || 0}
            </span>
          </div>
        </div>

        {/* ── which baladiyat this report went to ── */}
        <h3 className="detail-section-title">🏛️ Assigned baladiyat</h3>
        <div className="detail-list">
          {data.Assignments.map((assignment, index) => (//if a report is for multi baladeye show how handle it and how not
            <div key={index} className="detail-row">
              <span>{assignment.MunicipalityName}</span>
              <span>
                {assignment.rpa_IsHandler ? "✅ handling this report" : "not handling"}
                {assignment.rpa_Points !== 0 ? ` — ${assignment.rpa_Points} pts` : ""}
              </span>
            </div>
          ))}
        </div>

        {/* ── move to another baladiye, Admin only ──
            The component fetches its own list of baladiyat when it mounts. */}
        {role === "Admin" && (
          <MoveReportPanel reportId={id} onMoved={fetchReport} />
        )}

        {/* ── priority votes + the resident's agreement ──
            Always rendered: the tally is public. The component itself decides
            which buttons (if any) this role gets to see. */}
        <ReportPriorityVote
          reportId={id}
          report={report}
          priorityVotes={data.PriorityVotes}
          myPriorityVote={data.MyPriorityVote}
          myAgreement={data.MyAgreement}
          role={role}
          onVoted={fetchReport}
        />

        {/* ── the status trail ── */}
        <h3 className="detail-section-title">📜 Status history</h3>
        <div className="detail-list">
          {data.StatusHistory.length === 0 ? (
            <p className="detail-empty">No status changes yet.</p>
          ) : (
            data.StatusHistory.map((entry, index) => (
              <div key={index} className="detail-row">
                <span>
                  {entry.sth_OldStatus} → <strong>{entry.sth_NewStatus}</strong>
                </span>
                <span>
                  {entry.ChangedByName} ·{" "}
                  {new Date(entry.sth_ChangedAt).toLocaleString()}
                </span>
              </div>
            ))
          )}
        </div>

        {/* ── change status, Admin and Staff only ──
            Staff can only reach this page for their own baladiye's reports (the
            backend returns 403 otherwise), so no extra check is needed here.
            currentStatus is passed so the dropdown opens on the real value. */}
        {canEditStatus && (
          <ReportStatusPanel
            // `key` is not decoration here. ReportStatusPanel starts its
            // dropdown from currentStatus with useState, and useState only
            // reads its argument the FIRST time a component mounts — a later
            // prop change is ignored. Because the reload after a save is now
            // silent, the panel is never unmounted, so without this the
            // dropdown would keep showing the old value.
            // Changing `key` tells React "this is a different panel", and it
            // mounts a fresh one that re-reads the new status.
            key={report.rpt_Status}
            reportId={id}
            currentStatus={report.rpt_Status}
            currentPhotoUrl={report.rpt_ResolvedPhotoUrl}
            onSaved={fetchReport}
          />
        )}

        {/* ── comments ── */}
        <ReportComments
          reportId={id}
          comments={data.Comments}
          onPosted={fetchReport}
        />
      </div>
    </div>
  );
}

export default ReportDetail;
