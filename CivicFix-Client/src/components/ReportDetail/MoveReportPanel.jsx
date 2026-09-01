import { useState, useEffect } from "react";
import { readBody, errorTextOf } from "../../services/apiHelpers";

// NEW FILE — split out of ReportDetail.jsx.
//
// "↪️ Move to another baladiye" — Admin only.
//
// NOT the same as the "choose a handler" buttons on the Shared Reports tab.
// Those pick among the baladiyat the spatial query already found; this one can
// hand the report to ANY baladiye in the country. That is what you need when
// the automatic assignment was simply wrong — a bad boundary polygon, a GPS
// reading that drifted, or a problem that is really another baladiye's
// responsibility despite where it sits.
//
// Without it, the only way to correct a misplaced report was to delete it and
// ask the resident to file it again, losing its comments and votes.
//
// WHY IT MOVED OUT: it owned FOUR pieces of state and its own fetch, all of
// which only mattered for an Admin. It now loads its own list of baladiyat in
// its own useEffect, so the parent no longer runs an Admin-only fetch.
//
// Props:
//   reportId — for the PUT url
//   onMoved  — called after a successful move so the parent refetches
function MoveReportPanel({ reportId, onMoved }) {
  const [municipalities, setMunicipalities] = useState([]); // every baladiye, for the dropdown
  const [moveSearch, setMoveSearch] = useState(""); // what the admin typed, to narrow the list
  const [moveTargetId, setMoveTargetId] = useState(""); // the baladiye they picked
  const [moving, setMoving] = useState(false); // true while the move request runs
  const [error, setError] = useState("");

  // GET api/Municipalities — the same public endpoint the leaderboard uses.
  // Loaded once when this panel mounts. The parent only renders this component
  // for an Admin, so nobody else ever makes this request.
  useEffect(() => {
    const fetchMunicipalities = async () => {
      try {
        const response = await fetch("http://localhost:5140/api/Municipalities");
        if (response.ok) {
          setMunicipalities(await response.json());
        }
      } catch (err) {
        // ignore — without this list the dropdown just has nothing to pick from
      }
    };

    fetchMunicipalities();
  }, []); // [] = run once on mount, never again

  // PUT api/Reports/{id}/move  →  ReportsAdminController.MoveReport
  //
  // The backend replaces the report's assignments and, if the report was already
  // resolved, moves the +10 from the old baladiye to the new one.
  const moveReport = async () => {
    if (!moveTargetId) {
      setError("Choose a baladiye to move this report to.");
      return;
    }

    // find the name so the confirmation dialog can say where it is going
    const target = municipalities.find(
      (m) => String(m.mun_Id) === String(moveTargetId),
    );

    const confirmed = window.confirm(
      `Move this report to ${target ? target.mun_Name : "the selected baladiye"}?\n\n` +
        "It will be removed from its current baladiye. If the report was already " +
        "resolved, the points move across too.",
    );
    if (!confirmed) {
      return;
    }

    setMoving(true);
    setError("");
    try {
      const token = localStorage.getItem("token");
      const response = await fetch(
        `http://localhost:5140/api/Reports/${reportId}/move`,
        {
          method: "PUT",
          headers: {
            "Content-Type": "application/json",
            Authorization: `Bearer ${token}`,
          },
          body: JSON.stringify({ MunicipalityId: parseInt(moveTargetId) }),
        },
      );

      const body = await readBody(response);

      if (response.ok) {
        setMoveSearch("");
        setMoveTargetId("");
        onMoved(); // reload so the new baladiye shows in the assignments list
      } else {
        setError(errorTextOf(body, "Could not move this report."));
      }
    } catch (err) {
      setError("Could not connect to server.");
    } finally {
      setMoving(false);
    }
  };

  return (
    <div className="detail-move">
      <h3 className="detail-section-title">↪️ Move to another baladiye</h3>

      {/* there are hundreds of baladiyat, so type to narrow the list down
          before picking — a raw dropdown of them all is unusable */}
      <input
        className="form-input"
        type="text"
        placeholder="🔍 Type to search baladiyat..."
        value={moveSearch}
        onChange={(e) => setMoveSearch(e.target.value)}
      />

      <select
        className="form-input"
        value={moveTargetId}
        onChange={(e) => setMoveTargetId(e.target.value)}
      >
        <option value="">Select a baladiye...</option>
        {municipalities
          // filter by what was typed, case-insensitively
          .filter((m) => m.mun_Name.toLowerCase().includes(moveSearch.toLowerCase()))
          // cap the list so a huge dropdown does not slow the page down;
          // narrowing the search further is how you reach the rest
          .slice(0, 50)
          .map((m) => (
            <option key={m.mun_Id} value={m.mun_Id}>
              {m.mun_Name}
            </option>
          ))}
      </select>

      <button
        className="btn-save-status"
        disabled={moving || !moveTargetId}
        onClick={moveReport}
      >
        {moving ? "Moving..." : "Move report"}
      </button>

      <p className="detail-vote-panel__note">
        The report leaves its current baladiye and the new one becomes
        responsible for it. If it was already resolved, the points move too.
      </p>

      {error && <p className="report-status report-status--error">{error}</p>}
    </div>
  );
}

export default MoveReportPanel;
