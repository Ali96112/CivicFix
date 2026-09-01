import { useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  readBody,
  errorTextOf,
  getStatusClass,
  STATUS_OPTIONS,
} from "../../services/apiHelpers";

//   rep       — the report row from the API
//   role      — decides whether the admin controls appear
//   onChanged — a function from the parent used to reload the report list after something changes
function ReportCard({ rep, role, onChanged }) {
  const navigate = useNavigate();
  const [editing, setEditing] = useState(false);// is this card's change-status panel open?
  const [editStatus, setEditStatus] = useState(rep.rpt_Status);//store selected status
  const [editPhotoUrl, setEditPhotoUrl] = useState(rep.rpt_ResolvedPhotoUrl || "");//stores the resolved photo URL
  const [busy, setBusy] = useState(false);//prevents repeated clicks
  const [error, setError] = useState("");//store any error msg

  
  const saveStatus = async () => {//runs when admin change report status
    if (editStatus === "Resolved" && !editPhotoUrl.trim()) {
      setError("A photo of the fix is required before marking a report Resolved.");
      return;
    }
    setBusy(true);
    setError("");
    try {
      const token = localStorage.getItem("token");
      const response = await fetch(
        `http://localhost:5140/api/Reports/${rep.rpt_Id}/status`,
        {
          method: "PUT",
          headers: {
            "Content-Type": "application/json",
            Authorization: `Bearer ${token}`,
          },
          body: JSON.stringify({
            NewStatus: editStatus,
            ResolvedPhotoUrl: editPhotoUrl || null,
            ChangedByUserId: parseInt(localStorage.getItem("usr_Id")),
          }),
        },
      );

      const data = await readBody(response);

      if (response.ok) {
        setEditing(false);//closes the panel
        onChanged();//report reload
      } else {
        setError(errorTextOf(data, "Could not update the status."));
      }
    } catch (err) {
      setError("Could not connect to server.");
    } finally {
      setBusy(false);
    }
  };

  const deleteReport = async () => {//When the Admin clicks the trash button
    // this is destructive and cannot be undone, so it is worth one extra click
    const confirmed = window.confirm(
      `Delete "${rep.rpt_Title}" permanently?\n\nThis also removes its comments, status history and votes. It cannot be undone.`,
    );
    if (!confirmed) {
      return;
    }

    setBusy(true);//Usually this is used to disable the delete button so the user cannot click it multiple times.
    setError("");
    try {
      const token = localStorage.getItem("token");
      const response = await fetch(
        `http://localhost:5140/api/Reports/${rep.rpt_Id}`,
        { method: "DELETE", headers: { Authorization: `Bearer ${token}` } },
      );

      const data = await readBody(response);

      if (response.ok) {
        onChanged();
        
      } else {
        setError(errorTextOf(data, "Could not delete the report."));
      }
    } catch (err) {
      setError("Could not connect to server.");
    } finally {
      setBusy(false);
    }
  };

  // Admin pushes a shared report to one baladiye.
  // PUT api/Reports/{id}/assign-handler removes the other baladiyat entirely,
  // so the report ends up with exactly one owner and leaves the Shared tab.
  const assignHandler = async (municipalityId) => {//Assigning a shared report
    setBusy(true);
    setError("");
    try {
      const token = localStorage.getItem("token");
      const response = await fetch(
        `http://localhost:5140/api/Reports/${rep.rpt_Id}/assign-handler`,
        {
          method: "PUT",
          headers: {
            "Content-Type": "application/json",
            Authorization: `Bearer ${token}`,
          },
          body: JSON.stringify({ MunicipalityId: municipalityId }),
        },
      );

      const data = await readBody(response);

      if (response.ok) {
        onChanged();
      } else {
        setError(errorTextOf(data, "Could not assign this report."));
      }
    } catch (err) {
      setError("Could not connect to server.");
    } finally {
      setBusy(false);
    }
  };

  return (
    // the whole card is clickable and opens /report/<id>, the full detail page.
    // Works for every role; Staff opening another baladiye's report is stopped
    // by the backend, which answers 403 on that report's Id.
    <div
      className="report-item report-item--clickable"
      onClick={() => navigate(`/report/${rep.rpt_Id}`)}
    >
      {/* top row — category + status */}
      <div className="report-item__top">
          <span className="report-item__id">#{rep.rpt_Id}</span>
        <span className="report-item__category">{rep.CategoryName}</span>

        <span className="report-item__top-right">
          
            <span className={`report-badge ${getStatusClass(rep.rpt_Status)}`}>
              {rep.rpt_Status}
            </span>
          

          
          {role === "Admin" && (
            <button
              type="button"
              className="btn-delete"
              title="Delete this report permanently"
              disabled={busy}
              onClick={(e) => {
                e.stopPropagation();
                deleteReport();
              }}
            >
              🗑
            </button>
          )}
        </span>
      </div>

      
      

      <h3 className="report-item__title">{rep.rpt_Title}</h3>
      <p className="report-item__desc">{rep.rpt_Description.substring(0, 120) + "..."}</p>

     
      <div className="report-item__bottom">
        
       
        <span className="report-item__muni"> {/*the "shared" endpoint returns a Candidates array instead of the  AssignedMunicipalities string*/}
          🏛️{" "}
          {rep.Candidates
            ? rep.Candidates.map((c) => c.mun_Name).join(", ")
            : rep.AssignedMunicipalities}
        </span>
        <span className="report-item__date">
          📅 {new Date(rep.rpt_CreatedAt).toLocaleDateString()}
        </span>
        <span className="report-item__agree">👍 {rep.rpt_AgreementCount || 0}</span>
      </div>

      {rep.Candidates && (
        <div className="report-item__assign" onClick={(e) => e.stopPropagation()}>
          <p className="report-item__assign-label">
            {rep.NeedsDecision
              ? "⚠️ Shared between baladiyat — choose who handles it (the others are removed):"
              : "✅ Currently handled by the highlighted baladiye:"}
          </p>

          <div className="report-item__assign-buttons">
            {rep.Candidates.map((cand) => (
              <button
                key={cand.mun_Id}
                type="button"
                // the current owner gets the "chosen" style so the admin can see
                // at a glance which decision is already in place
                className={`btn-assign ${cand.IsHandler ? "btn-assign--chosen" : ""}`}
                disabled={busy || cand.IsHandler}
                onClick={() => assignHandler(cand.mun_Id)}
              >
                {cand.IsHandler ? "✓ " : "➡️ "}
                {cand.mun_Name}
              </button>
            ))}
          </div>
        </div>
      )}

      {/* any error from this card's own actions, shown on the card itself
          rather than at the top of the page where it is easy to miss */}
      {error && <p className="report-status report-status--error">{error}</p>}
    </div>
  );
}

export default ReportCard;
