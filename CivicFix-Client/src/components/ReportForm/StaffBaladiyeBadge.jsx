import { useState, useEffect } from "react";


function StaffBaladiyeBadge() {

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
      
      }
    };

    fetchMe();
  }, []); 

  if (!me) {
    return null; 
  }

  if (me.MunicipalityName) {
    return (
      <p className="report-header__baladiye">
        🏛️ Your baladiye: <strong>{me.MunicipalityName}</strong>
        <span className="report-header__points">{me.MunicipalityPoints} pts</span>
      </p>
    );
  }
}

export default StaffBaladiyeBadge;
