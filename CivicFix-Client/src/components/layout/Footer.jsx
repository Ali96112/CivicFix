import { useNavigate } from "react-router-dom";
import "../../styles/layout/Footer.css";

function Footer() {
  const navigate = useNavigate();

  // logged in → the reports page, logged out → register first.
  // Same rule the hero and CTA buttons use on the welcome page.
  const goToReport = () => {
    if (localStorage.getItem("token")) {
      navigate("/report");
    } else {
      navigate("/register");
    }
  };

  return (
    <footer className="footer">
      {/* top row — brand on left, links on right */}
      <div className="footer__top">
        <div className="footer__brand">
          Civic<span>Fix</span> 🇱🇧
        </div>

        <div className="footer__links">
          <a className="footer__link" onClick={() => navigate("/features")}>Features</a>
          <a className="footer__link" onClick={() => navigate("/about")}>About</a>
          <a className="footer__link" onClick={() => navigate("/dashboard")}>Dashboard</a>
          <a className="footer__link" onClick={() => navigate("/privacy")}>Privacy</a>
          <a className="footer__link" onClick={() => navigate("/contact")}>Contact</a>
        </div>
      </div>

      {/* bottom row — copyright on left, tagline on right */}
      <div className="footer__bottom">
        <span className="footer__copy">© 2026 CivicFix — Lebanon</span>
        <span className="footer__tagline">
          Digital infrastructure for Lebanon&apos;s municipalities
        </span>
      </div>
    </footer>
  );
}

export default Footer;