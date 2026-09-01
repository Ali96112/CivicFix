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
        <span className="report-item__agree"><AgreementIcon /> {rep.rpt_AgreementCount || 0}</span>
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
