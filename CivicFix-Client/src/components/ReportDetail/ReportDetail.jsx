import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import {readBody,errorTextOf,getStatusClass,} from "../../services/apiHelpers";
import Navbar from "../layout/Navbar";
import ReportStatusPanel from "./ReportStatusPanel";
import ReportPriorityVote from "./ReportPriorityVote";
import MoveReportPanel from "./MoveReportPanel";
import ReportComments from "./ReportComments";
import "../../styles/Report.css";

function ReportDetail() {
 const { id } = useParams();
  const navigate = useNavigate();

  const role = localStorage.getItem("usr_Role");
  const canEditStatus = role === "Admin" || role === "Staff";

  const myUserId = Number(localStorage.getItem("usr_Id"));

  const [data, setData] = useState(null); 
  const [loading, setLoading] = useState(true); 
  const [error, setError] = useState("");

  
  const [confirmBlock, setConfirmBlock] = useState(false);
  const [blocking, setBlocking] = useState(false);
  const [blockError, setBlockError] = useState("");

 
  const fetchReport = async (isFirstLoad = false) => {
   
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

  const blockReporter = async (reporterId) => {
    setBlocking(true);
    setBlockError("");
    try {
      const token = localStorage.getItem("token");
      const response = await fetch(
        `http://localhost:5140/api/Users/${reporterId}/block`,
        {
          method: "PUT",
          headers: { Authorization: `Bearer ${token}` },
        },
      );

      const body = await readBody(response);

      if (response.ok) {
        navigate("/report");
      } else {
        setBlockError(errorTextOf(body, "Could not block this user."));
        setConfirmBlock(false);
      }
    } catch (err) {
      setBlockError("Could not connect to server.");
      setConfirmBlock(false);
    } finally {
      setBlocking(false);
    }
  };

  
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

  const AgreementIcon = () => (
    <svg
      height="1em"
      width="1em"
      viewBox="0 0 24 24"
      xmlns="http://www.w3.org/2000/svg"
      style={{ verticalAlign: "middle", fill: "black" }}
    >
      <path d="m19.954 8.641h-5.716l1.256-3.141a2.413 2.413 0 0 0 -.534-2.607 1.824 1.824 0 0 0 -2.776.23l-3.323 4.651a15.386 15.386 0 0 1 -1.65 1.949l-.421.421a2.5 2.5 0 0 0 -4.79 1v8a2.5 2.5 0 0 0 4.79 1l.549.55a3.237 3.237 0 0 0 2.3.953h6.481a2.627 2.627 0 0 0 2.4-1.558l3.18-7.157a3.527 3.527 0 0 0 .3-1.432v-.811a2.049 2.049 0 0 0 -2.046-2.048zm-15.454 12a1.5 1.5 0 0 1 -1.5-1.5v-8a1.5 1.5 0 0 1 3 0v8a1.5 1.5 0 0 1 -1.5 1.5zm16.5-9.141a2.514 2.514 0 0 1 -.218 1.028l-3.182 7.149a1.624 1.624 0 0 1 -1.484.964h-6.474a2.245 2.245 0 0 1 -1.6-.661l-1.042-1.046v-7.586l.918-.918a16.466 16.466 0 0 0 1.758-2.075l3.324-4.65a.82.82 0 0 1 .6-.343.831.831 0 0 1 .652.239 1.415 1.415 0 0 1 .313 1.528l-1.53 3.827a.5.5 0 0 0 .464.685h6.454a1.047 1.047 0 0 1 1.047 1.046z" />
    </svg>
  );
  const DisagreementIcon = () => (
    <svg
      height="1em"
      width="1em"
      viewBox="0 0 64 64"
      xmlns="http://www.w3.org/2000/svg"
      style={{ verticalAlign: "middle", fill: "black" }}
    >
      <path d="m56 5.533h-11a1.5 1.5 0 0 0 -1.261.692l-5.068-2.534a1.5 1.5 0 0 0 -.671-.158h-18.649a10.742 10.742 0 0 0 -10.062 6.867l-4.361 11.147a35.123 35.123 0 0 0 -2.428 12.863 6.129 6.129 0 0 0 6.122 6.122h14.138c-1.214 3.107-3.26 9.011-3.26 13.5 0 2.908 1.521 5.282 4.4 6.867a4.829 4.829 0 0 0 2.335.6 4.934 4.934 0 0 0 2.018-.437 4.786 4.786 0 0 0 2.739-3.323c2.108-9.261 9.876-15.258 13.195-17.449a1.491 1.491 0 0 0 .813.242h11a1.5 1.5 0 0 0 1.5-1.5v-32a1.5 1.5 0 0 0 -1.5-1.499zm-27.934 51.544a1.806 1.806 0 0 1 -1.042 1.252 1.858 1.858 0 0 1 -1.677-.058c-1.916-1.054-2.847-2.441-2.847-4.239 0-5.607 3.832-14.3 3.871-14.39a1.5 1.5 0 0 0 -1.371-2.11h-16.378a3.125 3.125 0 0 1 -3.122-3.122 32.155 32.155 0 0 1 2.221-11.77l4.361-11.147a7.762 7.762 0 0 1 7.269-4.96h18.3l5.683 2.842a1.61 1.61 0 0 0 .171.064v27.726c-2.916 1.752-12.835 8.462-15.439 19.912zm26.434-19.545h-8v-29h8z" />
    </svg>
  );
 
  

  return (
    <div className="detail-page">
      <Navbar />

      <div className="detail-container">
         <button className="btn-back" onClick={() => navigate("/report")}>← Back</button>
        {error && <p className="report-status report-status--error">{error}</p>}

        {/* ── headline ── */}
        <div className="detail-head">
          <span className="detail-head__id">#{report.rpt_Id}</span>
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
            <p className="detail-photo__label">
              After the fix / صورة بعد الإصلاح
            </p>
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
              {role === "Admin" &&
                Number(report.rpt_ReporterId) !== myUserId &&
                report.ReporterRole !== "Admin" && (
                  <span className="block-user">
                    {confirmBlock ? (
                      <>
                        <button
                          className="block-user__confirm"
                          disabled={blocking}
                          onClick={() => blockReporter(report.rpt_ReporterId)}
                        >
                          {blocking
                            ? "Blocking..."
                            : "Confirm — deletes all their reports"}
                        </button>
                        <button
                          className="block-user__cancel"
                          disabled={blocking}
                          onClick={() => setConfirmBlock(false)}
                        >
                          Cancel
                        </button>
                      </>
                    ) : (
                      <button
                        className="block-user__btn"
                        onClick={() => {
                          setBlockError("");
                          setConfirmBlock(true);
                        }}
                      >
                        Block user
                      </button>
                    )}
                  </span>
                )}
            </span>
          </div>

          {blockError ? (
            <div className="detail-fact">
              <span className="block-user__error">{blockError}</span>
            </div>
          ) : null}
          <div className="detail-fact">
            <span className="detail-fact__key">Date</span>
            <span className="detail-fact__value">
              {new Date(report.rpt_CreatedAt).toLocaleString()}
            </span>
          </div>
          <div className="detail-fact">
            <span className="detail-fact__key">Priority</span>
            <span className="detail-fact__value">
              {report.rpt_Priority || "Not set"}
            </span>
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
              <AgreementIcon /> {report.rpt_AgreementCount || 0} &nbsp;{" "}
              <DisagreementIcon /> {report.rpt_DisagreementCount || 0}
            </span>
          </div>
        </div>

        {/* ── which baladiyat this report went to ── */}
        <h3 className="detail-section-title">🏛️ Assigned baladiyat</h3>
        <div className="detail-list">
          {data.Assignments.map(
            (
              assignment,
              index, 
            ) => (
              <div key={index} className="detail-row">
                <span>{assignment.MunicipalityName}</span>
                <span>
                  {assignment.rpa_IsHandler
                    ? "✅ handling this report"
                    : "not handling"}
                </span>
              </div>
            ),
          )}
        </div>

        
        {role === "Admin" && (
          <MoveReportPanel reportId={id} onMoved={fetchReport} />
        )}

        
        <ReportPriorityVote
          reportId={id}
          report={report}
          priorityVotes={data.PriorityVotes}
          myPriorityVote={data.MyPriorityVote}
          myAgreement={data.MyAgreement}
          role={role}
          onVoted={fetchReport}
        />

        
        <h3 className="detail-section-title"> Status history</h3>
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

        {canEditStatus && (
          <ReportStatusPanel
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
