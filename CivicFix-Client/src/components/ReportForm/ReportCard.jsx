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
        <span className="report-item__category">{rep.CategoryName}</span>

        <span className="report-item__top-right">
          {/* the status badge is a BUTTON for an Admin, so clicking it opens the
              change-status panel below. For everyone else it stays a plain badge. */}
          {role === "Admin" ? (
            <button
              type="button"
              className={`report-badge report-badge--button ${getStatusClass(rep.rpt_Status)}`}
              title="Click to change the status"
              onClick={(e) => {
                // the card itself navigates to the detail page, so every control
                // inside it must stop the click bubbling up — otherwise pressing
                // this would navigate away before the panel could open
                e.stopPropagation();
                setEditStatus(rep.rpt_Status);
                setEditPhotoUrl(rep.rpt_ResolvedPhotoUrl || "");
                setEditing(!editing);
              }}
            >
              {rep.rpt_Status} ✎
            </button>
          ) : (
            <span className={`report-badge ${getStatusClass(rep.rpt_Status)}`}>
              {rep.rpt_Status}
            </span>
          )}

          {/* the small delete button, Admin only */}
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

      {/* the Admin's change-status panel, only when this card's badge was clicked */}
      {role === "Admin" && editing && (
        <div className="report-item__edit" onClick={(e) => e.stopPropagation()}>
          <label className="form-label">Change status</label>
          <select
            className="form-input"
            value={editStatus}
            onChange={(e) => setEditStatus(e.target.value)}
          >
            {STATUS_OPTIONS.map((statusOption) => (
              <option key={statusOption} value={statusOption}>
                {statusOption}
              </option>
            ))}
          </select>

          {/* the backend requires a proof photo before it accepts "Resolved",
              so the field only appears when it is needed */}
          {editStatus === "Resolved" && (
            <>
              <label className="form-label">Photo of the fix (required)</label>
              <input
                className="form-input"
                type="text"
                placeholder="Paste a link to the after photo"
                value={editPhotoUrl}
                onChange={(e) => setEditPhotoUrl(e.target.value)}
              />
            </>
          )}

          <div className="report-item__edit-actions">
            <button
              type="button"
              className="btn-save-status"
              disabled={busy}
              onClick={saveStatus}
            >
              {busy ? "Saving..." : "Save status"}
            </button>
            <button
              type="button"
              className="btn-cancel-status"
              onClick={() => setEditing(false)}
            >
              Cancel
            </button>
          </div>
        </div>
      )}

      <h3 className="report-item__title">{rep.rpt_Title}</h3>
      <p className="report-item__desc">{rep.rpt_Description}</p>

      {/* bottom row — baladiye + date + agreement count */}
      <div className="report-item__bottom">
        {/* the "shared" endpoint returns a Candidates array instead of the
            AssignedMunicipalities string the other two endpoints return, so we
            build the names from whichever one this report actually has */}
        <span className="report-item__muni">
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

      {/*
        The admin's decision panel. Only rendered on the shared tab, where the
        backend attached a Candidates array to each report.

        One button per baladiye the report touches. Clicking one calls
        PUT api/Reports/{id}/assign-handler, and that baladiye becomes the single
        owner — the others are REMOVED from the report by the backend, so it
        stops being shared and disappears from this tab. To change it afterwards,
        open the report and use "↪️ Move to another baladiye".
      */}
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
