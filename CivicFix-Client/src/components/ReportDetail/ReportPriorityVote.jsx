import { useState } from "react";
import { readBody, errorTextOf } from "../../services/apiHelpers";

//these props give the access to parent components
function ReportPriorityVote({reportId,report,priorityVotes,myPriorityVote,myAgreement,role,onVoted,})
 {
  const [voting, setVoting] = useState(false);
  const [error, setError] = useState("");

  
  const votePriority = async (priority) => {
    setVoting(true);
    setError("");
    try {
      const token = localStorage.getItem("token");
      const response = await fetch(
        `http://localhost:5140/api/Reports/${reportId}/priority`,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            Authorization: `Bearer ${token}`,
          },
          body: JSON.stringify({
            Priority: priority, 
            UserId: parseInt(localStorage.getItem("usr_Id")), 
          }),
        },
      );
      const body = await readBody(response);
      if (response.ok) {
        onVoted(); 
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



  const submitAgreement = async (isAgreement) => {
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
            IsAgreement: isAgreement, 
            UserId: parseInt(localStorage.getItem("usr_Id")), // ignored, the JWT decides
          }),
        },
      );

      const body = await readBody(response);

      if (response.ok) {
        onVoted(); 
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
    
      
      
      {role === "Resident" && report.ReporterRole === "Resident" && (
      <div className="detail-vote-panel">
        <h3 className="detail-section-title">🗳️ Priority votes</h3>
      <div className="detail-votes">
        <span>🔴 High: {priorityVotes.High}</span>
        <span>🟡 Medium: {priorityVotes.Medium}</span>
        <span>🟢 Low: {priorityVotes.Low}</span>
        <span>Total: {priorityVotes.Total}</span>
      </div>

         
          <p className="detail-vote-panel__ask">
            {myPriorityVote
              ? "Your vote /  تصويتك"
              : "How urgent is this? / ما مدى إلحاح هذه المشكلة؟"}
          </p>

          <div className="detail-vote-panel__buttons">
            {["Low", "Medium", "High"].map((option) => (
              <button
                key={option}
                className={`btn-vote btn-vote--${option.toLowerCase()} ${
                  myPriorityVote === option ? "btn-vote--chosen" : ""
                }`}
                
                disabled={voting || myPriorityVote === option}
                onClick={() => votePriority(option)}
              >
                {option === "Low" ? "🟢" : option === "Medium" ? "🟡" : "🔴"} {option}
                {myPriorityVote === option ? " ✔" : ""}
              </button>
            ))}
          </div>
        </div>
      )}

      
      {role === "Resident" && report.ReporterRole === "Staff" && (
        <div className="detail-vote-panel detail-vote-panel--agree">
          {myAgreement !== null && myAgreement !== undefined ? (
            <p className="detail-vote-panel__done">
              {myAgreement
                ? "✔ You confirmed this work was done."
                : "✔ You reported that this work was NOT done."}{" "}
              So far {report.rpt_AgreementCount || 0} confirmed and{" "}
              {report.rpt_DisagreementCount || 0} disputed it.
            </p>
          ) : (
            <>
              <p className="detail-vote-panel__ask">
                Did the baladiye really do this work? / هل قامت البلدية بهذا العمل فعلاً؟
              </p>
              <div className="detail-vote-panel__buttons">
                <button
                  className="btn-vote btn-vote--agree"
                  disabled={voting}
                  onClick={() => submitAgreement(true)}
                >
                  Yes, it was done
                </button>
                <button
                  className="btn-vote btn-vote--disagree"
                  disabled={voting}
                  onClick={() => submitAgreement(false)}
                >
                  No, it was not
                </button>
              </div>
              
            </>
          )}
        </div>
      )}

     
      {error && <p className="report-status report-status--error">{error}</p>}
    </>
  );
}

export default ReportPriorityVote;
