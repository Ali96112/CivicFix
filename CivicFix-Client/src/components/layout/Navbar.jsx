import { useNavigate, useLocation } from "react-router-dom";
import "../../styles/layout/Navbar.css";

function Navbar() {
  const navigate = useNavigate();

  const isLoggedIn = localStorage.getItem("token");
  const fullName = localStorage.getItem("usr_FullName");
  const role = localStorage.getItem("usr_Role");

  const handleLogout = () => {
    localStorage.removeItem("token");
    localStorage.removeItem("usr_Id");
    localStorage.removeItem("usr_FullName");
    localStorage.removeItem("usr_Role");
    navigate("/");
    window.location.reload();
  };

    // logged in → the reports page, logged out → register first
  const goToReport = () => {
    if (isLoggedIn) {
      navigate("/report");
    } else {
      navigate("/register");
    }
  };

  

  return (
    <nav className="navbar">
      <div className="navbar__brand" onClick={() => navigate("/")}>
        <div className="navbar__logo">🏙️</div>
        <span className="navbar__name">
          Civic<span>Fix</span>
        </span>
      </div>

                 <div className="navbar__links">
        <a className="navbar__link" onClick={() => navigate("/map")}>Map</a>
        <a className="navbar__link" onClick={() => navigate("/features")}>Features</a>
        <a className="navbar__link" onClick={() => navigate("/about")}>About</a>
        <a className="navbar__link" onClick={goToReport}>My Reports</a>
        <a className="navbar__link" onClick={() => navigate("/dashboard")}>Dashboard</a>
        
      </div>

      <div className="navbar__buttons">
        {isLoggedIn ? (
          <>
            {fullName && (
              <span className="navbar__user">
                👤 {fullName}
                <span className="navbar__role">{role}</span>
              </span>
            )}
            <button className="btn-red" onClick={handleLogout}>
              Logout / تسجيل الخروج
            </button>
          </>
        ) : (
          <>
            <button className="btn-outline" onClick={() => navigate("/login")}>
              Login
            </button>
            <button className="btn-red" onClick={() => navigate("/register")}>
              Register
            </button>
          </>
        )}
      </div>
    </nav>
  );
}

export default Navbar;
