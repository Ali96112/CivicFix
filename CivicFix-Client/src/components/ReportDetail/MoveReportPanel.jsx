import { useState, useEffect } from "react";
import { readBody, errorTextOf } from "../../services/apiHelpers";

function MoveReportPanel({ reportId, onMoved }) {//when onMoved true =fetchreport we used it here since when we move to other baladeye  we get the new version of this report it is refteched
  const [municipalities, setMunicipalities] = useState([]); 
  const [moveSearch, setMoveSearch] = useState(""); // what the admin typed, to narrow the list
  const [moveTargetId, setMoveTargetId] = useState(""); // the baladiye the admin picked
  const [moving, setMoving] = useState(false); // when true change the button text
  const [error, setError] = useState("");

  useEffect(() => {
    const fetchMunicipalities = async () => {
      try {
        const response = await fetch("http://localhost:5140/api/Municipalities");
        if (response.ok) {
          setMunicipalities(await response.json());//so now muncipalities has list of all muncipalities
        }
      } catch (err) {
        // ignore — without this list the dropdown just has nothing to pick from
      }
    };

    fetchMunicipalities();
  }, []); 

  const moveReport = async () => {//this function goal is to Move the current report to the municipality the admin selected
    if (!moveTargetId) {
      setError("Choose a baladiye to move this report to.");
      return;
    }

    const target = municipalities.find(
      (m) => String(m.mun_Id) === String(moveTargetId),//Is this municipality's ID equal to the ID the admin selected?
    );

    const confirmed = window.confirm(//popup msg
      `Move this report to ${target ? target.mun_Name : "the selected baladiye"}?\n\n` +
        "It will be removed from its current baladiye. If the report was already " +
        "resolved, the points move across too.",
    );
    if (!confirmed) {
      return;
    }
//if confirmed
    setMoving(true);//changing button text
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
