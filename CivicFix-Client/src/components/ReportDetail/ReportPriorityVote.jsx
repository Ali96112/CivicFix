import { useState } from "react";
import { readBody, errorTextOf } from "../../services/apiHelpers";

//these props give the access to parent components
function ReportPriorityVote({reportId,report,priorityVotes,myPriorityVote,myAgreement,role,onVoted,})
 {
  // true while a vote OR an agreement is being sent, so the buttons can be
  // disabled and cannot be double-clicked into a 400
  const [voting, setVoting] = useState(false);
  const [error, setError] = useState("");

  // POST api/Reports/{id}/priority  →  ReportsFeedbackController.VoteOnPriority
  const votePriority = async (priority) => {//priority is the value passed when a button is clicked.
    setVoting(true);//So while the API request is running, the buttons become disabled.
    setError("");
    try {
      const token = localStorage.getItem("token");//You need this because your backend uses:
      const response = await fetch(
        `http://localhost:5140/api/Reports/${reportId}/priority`,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            Authorization: `Bearer ${token}`,
          },
          body: JSON.stringify({
            Priority: priority, // must be exactly "Low", "Medium" or "High"
            UserId: parseInt(localStorage.getItem("usr_Id")), // ignored, the JWT decides
          }),
        },
      );
      const body = await readBody(response);
      if (response.ok) {
        onVoted(); // reload so the new tally and the winning priority show
      } else {
        setError(errorTextOf(body, "Could not submit your priority vote."));
      }
    } catch (err) {
      setError("Could not connect to server.");
    } finally {
      setVoting(false);
    }
  };
  /*output:{
  "Priority": "High",
  "UserId": 7
    }*/

  // POST api/Reports/{id}/agree  →  ReportsFeedbackController.AgreeOnReport

  const submitAgreement = async (isAgreement) => {//This function sends the resident’s Yes(agree) or No(not agreed) answer to the backend
    setVoting(true);//disable the button
    setError("");
    try {
      const token = localStorage.getItem("token");
      const response = await fetch(
        `http://localhost:5140/api/Reports/${reportId}/agree`,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            Authorization: `Bearer ${token}`,
          },
          body: JSON.stringify({
            IsAgreement: isAgreement, // true = "yes they did it", false = "no"
            UserId: parseInt(localStorage.getItem("usr_Id")), // ignored, the JWT decides
          }),
        },
      );

      const body = await readBody(response);

      if (response.ok) {
        onVoted(); // reload so the 👍/👎 counts update
      } else {
        setError(errorTextOf(body, "Could not submit your answer."));
      }
    } catch (err) {
      setError("Could not connect to server.");
    } finally {
      setVoting(false);
    }
  };

  return (
    <>
      {/* ── the tally, visible to every role ── */}
      <h3 className="detail-section-title">🗳️ Priority votes</h3>
      <div className="detail-votes">
        <span>🔴 High: {priorityVotes.High}</span>
        <span>🟡 Medium: {priorityVotes.Medium}</span>
        <span>🟢 Low: {priorityVotes.Low}</span>
        <span>Total: {priorityVotes.Total}</span>
      </div>

      {/*
        A Resident votes on how urgent this report is.
        Only shown when BOTH are true:
          - I am a Resident
          - this report was submitted by a Resident
      */}
      {role === "Resident" && report.ReporterRole === "Resident" && (
        <div className="detail-vote-panel">
          {/*The buttons STAY on screen after you have voted, so you can changeyour mind.*/}
          <p className="detail-vote-panel__ask">
            {myPriorityVote
              ? "Change your vote / غيّر تصويتك"
              : "How urgent is this? / ما مدى إلحاح هذه المشكلة؟"}
          </p>

          <div className="detail-vote-panel__buttons">
            {["Low", "Medium", "High"].map((option) => (//creating the three buttons
              <button
                key={option}
                // the option you currently hold gets the --chosen style, so you
                // can see what you picked without a separate line of text
                className={`btn-vote btn-vote--${option.toLowerCase()} ${
                  myPriorityVote === option ? "btn-vote--chosen" : ""
                }`}
                
                disabled={voting || myPriorityVote === option}//. It disables only the selected button; the other two remain clickable.
                onClick={() => votePriority(option)}//When the user clicks a button, its option is sent to votePriority()
              >
                {option === "Low" ? "🟢" : option === "Medium" ? "🟡" : "🔴"} {option}
                {myPriorityVote === option ? " ✔" : ""}{/*If this is the priority I already voted for, show a checkmark. */}
              </button>
            ))}
          </div>
        </div>
      )}

      
      {role === "Resident" && report.ReporterRole === "Staff" && (
        <div className="detail-vote-panel detail-vote-panel--agree">
          {myAgreement !== null && myAgreement !== undefined ? (//we write it like this not using false since in our case false is a value
            <p className="detail-vote-panel__done">
              {myAgreement
                ? "✔ You confirmed this work was done."
                : "✔ You reported that this work was NOT done."}{" "}
              So far {report.rpt_AgreementCount || 0} confirmed and{" "}{/*shows the current total */}
              {report.rpt_DisagreementCount || 0} disputed it.
            </p>
          ) : (
            <>
              <p className="detail-vote-panel__ask">
                Did the baladiye really do this work? / هل قامت البلدية بهذا العمل فعلاً؟
              </p>
              <div className="detail-vote-panel__buttons">{/*here are the two agrement buttons */}
                <button
                  className="btn-vote btn-vote--agree"
                  disabled={voting}
                  onClick={() => submitAgreement(true)}//So the frontend sends: IsAgreement = true to backend
                >
                  👍 Yes, it was done
                </button>
                <button
                  className="btn-vote btn-vote--disagree"
                  disabled={voting}
                  onClick={() => submitAgreement(false)}
                >
                  👎 No, it was not
                </button>
              </div>
              
            </>
          )}
        </div>
      )}

      {/* errors from this panel's own requests, shown here rather than at the
          top of the page */}
      {error && <p className="report-status report-status--error">{error}</p>}
    </>
  );
}

export default ReportPriorityVote;
