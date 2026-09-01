import { useNavigate } from "react-router-dom";

// The top bar: logo, who is signed in, and the Dashboard / Logout buttons.
//
// Split out of ReportForm.jsx. ReportDetail.jsx had a near-identical copy of
// this markup, so the two were free to drift apart — now there is one.
//
// Takes no props: everything it needs is in localStorage, saved at login.
function ReportNavbar({ backLabel, backTo }) {
  const navigate = useNavigate();

  const role = localStorage.getItem("usr_Role");

  // ADDED — the logged-in user's name, for the navbar greeting.
  //
  // NOTE: this does NOT need the /me endpoint. LoginForm and RegisterForm already
  // save usr_FullName to localStorage when you sign in, so the name is sitting
  // right here for free. A person's name is exactly the kind of thing that
  // belongs in login storage: it identifies them and it does not change while
  // they are using the app — unlike their baladiye's points, which do.
  const fullName = localStorage.getItem("usr_FullName");

  const logout = () => {
    // clears everything login stored, then goes home.
    // Without this there was no way to switch accounts except clearing browser
    // storage by hand, which matters a lot when you are demoing three roles.
    localStorage.removeItem("token");
    localStorage.removeItem("usr_Id");
    localStorage.removeItem("usr_FullName");
    localStorage.removeItem("usr_Role");
    navigate("/");
  };

  return (
    <nav className="report-nav">
      <div className="report-nav__brand" onClick={() => navigate("/")}>
        <div className="report-nav__logo">🏙️</div>
        <span className="report-nav__name">
          Civic<span>Fix</span>
        </span>
      </div>

      {/* who is signed in, shown for EVERY role.
          The name comes from localStorage, not from an API call. */}
      <div className="report-nav__right">
        {fullName && (
          <span className="report-nav__user">
            👤 {fullName}
            <span className="report-nav__role">{role}</span>
          </span>
        )}

        {/* the reports page shows "Dashboard"; the detail page passes
            backLabel="← Back to reports" and backTo="/report" instead */}
        <button
          className="report-nav__btn"
          onClick={() => navigate(backTo || "/dashboard")}
        >
          {backLabel || "📊 Dashboard"}
        </button>

        <button className="report-nav__btn report-nav__btn--logout" onClick={logout}>
          Logout
        </button>
      </div>
    </nav>
  );
}

export default ReportNavbar;
