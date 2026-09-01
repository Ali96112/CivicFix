import { useState } from "react";
import { readBody, errorTextOf, uploadPhoto, STATUS_OPTIONS } from "../../services/apiHelpers";

// NEW FILE — split out of ReportDetail.jsx.
//
// The "✎ Change status" box at the bottom of a report page. Admin and Staff only.
//
// WHY IT MOVED OUT: it owned three pieces of state (newStatus, resolvedPhotoFile,
// saving) and a 45-line saveStatus function, none of which any other part of the
// detail page ever read. Keeping them in the parent meant that anyone reading
// ReportDetail had to scroll past all of it to reach the next feature.
//
// Staff can only reach this page for their OWN baladiye's reports — the backend
// answers 403 on GetReportById otherwise — so there is no extra role check here
// beyond Admin-or-Staff, which the parent already made before rendering this.
//
// Props:
//   reportId          — which report, for the PUT url
//   currentStatus     — the report's status right now; the dropdown starts here
//   currentPhotoUrl   — the fix photo already saved, if any (kept when no new
//                       file is chosen, so saving twice does not wipe it)
//   onSaved           — called after a successful save so the parent refetches
function ReportStatusPanel({ reportId, currentStatus, currentPhotoUrl, onSaved }) {
  // the dropdown's value. Starts on whatever the report is now, so pressing
  // Save without touching anything is a no-op instead of a surprise change.
  const [newStatus, setNewStatus] = useState(currentStatus);

  // the chosen file itself, NOT a url. It is uploaded inside saveStatus and the
  // url the API gives back is what actually goes into the report row.
  const [resolvedPhotoFile, setResolvedPhotoFile] = useState(null);

  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  // PUT api/Reports/{id}/status  →  ReportsAdminController.UpdateReportStatus
  const saveStatus = async () => {
    // the backend refuses "Resolved" without a photo of the fix. Catching it
    // here gives a clear message instead of a round trip that fails.
    // currentPhotoUrl counts too — a report resolved earlier already has one.
    if (newStatus === "Resolved" && !resolvedPhotoFile && !currentPhotoUrl) {
      setError("Choose a photo of the fix before marking this report Resolved.");
      return;
    }

    setSaving(true);
    setError("");
    try {
      // if a new file was chosen, upload it FIRST and use the url it returns.
      // otherwise keep whatever photo the report already had.
      let photoUrl = currentPhotoUrl;
      if (resolvedPhotoFile) {
        photoUrl = await uploadPhoto(resolvedPhotoFile);
      }

      const token = localStorage.getItem("token");
      const response = await fetch(
        `http://localhost:5140/api/Reports/${reportId}/status`,
        {
          method: "PUT",
          headers: {
            "Content-Type": "application/json",
            Authorization: `Bearer ${token}`,
          },
          body: JSON.stringify({
            NewStatus: newStatus,
            ResolvedPhotoUrl: photoUrl || null,
            // ignored by the backend now (it trusts the JWT instead, so nobody
            // can blame a status change on another user), sent only so the
            // request shape stays the same
            ChangedByUserId: parseInt(localStorage.getItem("usr_Id")),
          }),
        },
      );

      const body = await readBody(response);

      if (response.ok) {
        setResolvedPhotoFile(null);
        onSaved(); // parent refetches: new status + a new status-history row
      } else {
        setError(errorTextOf(body, "Could not update the status."));
      }
    } catch (err) {
      // uploadPhoto throws with the API's own reason ("too large", "only images")
      setError(err.message || "Could not connect to server.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="detail-edit">
      <h3 className="detail-section-title">✎ Change status</h3>

      <select
        className="form-input"
        value={newStatus}
        onChange={(e) => setNewStatus(e.target.value)}
      >
        {STATUS_OPTIONS.map((option) => (
          <option key={option} value={option}>
            {option}
          </option>
        ))}
      </select>

      {/* the proof photo is only required when resolving, so the field only
          appears for that one status */}
      {newStatus === "Resolved" && (
        <div className="form-group">
          <label className="form-label">Photo of the fix</label>
          <input
            className="form-input"
            type="file"
            accept="image/*"
            onChange={(e) => setResolvedPhotoFile(e.target.files[0])}
          />
        </div>
      )}

      <button className="btn-save-status" disabled={saving} onClick={saveStatus}>
        {saving ? "Saving..." : "Save status"}
      </button>

      {/* the error is shown inside this panel now, next to the button that
          caused it, instead of at the top of the page where it is easy to miss */}
      {error && <p className="report-status report-status--error">{error}</p>}
    </div>
  );
}

export default ReportStatusPanel;
