import { useState } from "react";
import { readBody, errorTextOf, uploadPhoto, STATUS_OPTIONS } from "../../services/apiHelpers";

function ReportStatusPanel({ reportId, currentStatus, currentPhotoUrl, onSaved }) {
  
  const [newStatus, setNewStatus] = useState(currentStatus);

  const [resolvedPhotoFile, setResolvedPhotoFile] = useState(null);

  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  // PUT api/Reports/{id}/status  →  ReportsAdminController.UpdateReportStatus
  const saveStatus = async () => {
    
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
