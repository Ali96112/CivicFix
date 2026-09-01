import { useState, useEffect } from "react";

// The "🏛️ Your baladiye: Beirut — 140 pts" line under the page title.

function StaffBaladiyeBadge() {
  // the logged-in user's own record from GET api/Users/me.
  // localStorage only holds the name and role — it never held the baladiye,
  // so a Staff member had no way of seeing which baladiye they work for.
  const [me, setMe] = useState(null);

  useEffect(() => {
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

  if (me.MunicipalityName) {
    return (
      <p className="report-header__baladiye">
        🏛️ Your baladiye: <strong>{me.MunicipalityName}</strong>
        <span className="report-header__points">{me.MunicipalityPoints} pts</span>
      </p>
    );
  }
  return (
    <p className="report-header__baladiye report-header__baladiye--missing">
      ⚠️ Your account is not assigned to any baladiye yet — ask an admin to set
      it, or you will not be able to see or submit reports.
    </p>
  );
}

export default StaffBaladiyeBadge;
