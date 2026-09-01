import { useState, useEffect } from "react";

// The "🏛️ Your baladiye: Beirut — 140 pts" line under the page title.
//
// Split out of ReportForm.jsx. It fetches its own data rather than being handed
// it, because nothing else on the page uses that data — keeping the request next
// to the only thing that displays it means neither can be forgotten.
//
// Render it only for Staff. The parent decides that; this component does not
// check the role itself, so it stays a dumb display piece.
function StaffBaladiyeBadge() {
  // the logged-in user's own record from GET api/Users/me.
  // localStorage only holds the name and role — it never held the baladiye,
  // so a Staff member had no way of seeing which baladiye they work for.
  const [me, setMe] = useState(null);

  useEffect(() => {
    // Failing quietly is fine here: if this call does not come back, the page
    // still works, it just does not show the baladiye line.
    const fetchMe = async () => {
      try {
        const token = localStorage.getItem("token");
        const response = await fetch("http://localhost:5140/api/Users/me", {
          headers: { Authorization: `Bearer ${token}` },
        });
        if (response.ok) {
          setMe(await response.json());
        }
      } catch (err) {
        // ignore — the baladiye line is nice to have, not essential
      }
    };

    fetchMe();
  }, []); // once, when the badge appears

  if (!me) {
    return null; // nothing to show until the answer arrives
  }

  // The baladiye name. This matters because everything a Staff user sees and can
  // change is filtered by this one baladiye, so it should never be a guess.
  // The points are the same score shown on the public leaderboard.
  if (me.MunicipalityName) {
    return (
      <p className="report-header__baladiye">
        🏛️ Your baladiye: <strong>{me.MunicipalityName}</strong>
        <span className="report-header__points">{me.MunicipalityPoints} pts</span>
      </p>
    );
  }

  // The "no baladiye" warning.
  // A Staff account with usr_MunicipalityId = NULL cannot do anything: the
  // backend rejects their reports with "Staff member is not assigned to any
  // baladiye" and their report list comes back empty. Without this line the
  // page just looks broken for no visible reason.
  return (
    <p className="report-header__baladiye report-header__baladiye--missing">
      ⚠️ Your account is not assigned to any baladiye yet — ask an admin to set
      it, or you will not be able to see or submit reports.
    </p>
  );
}

export default StaffBaladiyeBadge;
