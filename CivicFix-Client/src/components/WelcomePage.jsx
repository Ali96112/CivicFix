import { useNavigate } from "react-router-dom"; // useNavigate is a function that lets us navigate between pages /login, /register
import "../styles/Welcome.css";

// data arrays — defined outside the function so they're not recreated every render in memory
//array of objects with varable names
const STATS = [
  { num: "1,432+", label: "Municipalities", arabic: "بلدية جاهزة", icon: "🏛️" },
  {
    num: "~4M",
    label: "Unheard citizens",
    arabic: "مواطن بدون قناة",
    icon: "🗣️",
  },
  {
    num: "0",
    label: "National platforms",
    arabic: "منصات وطنية اليوم",
    icon: "📵",
  },
  {
    num: "<72h",
    label: "Target fix time",
    arabic: "وقت الحلّ المستهدف",
    icon: "⚡",
  },
];
const STEPS = [
  {
    num: "01",
    icon: "📍",
    color: "#C8102E",
    bg: "#fff0f2",
    tag: "Citizen Reports",
    title: "المواطن يبلّغ",
    desc: "A resident submits an issue — photo, location, description. CivicFix finds their municipality automatically.",
  },
  {
    num: "02",
    icon: "🏛️",
    color: "#7c3aed",
    bg: "#f5f3ff",
    tag: "Baladiye Receives",
    title: "البلدية تستلم",
    desc: "The report arrives in the municipality's dashboard — categorized, prioritized, and ready to assign to a team.",
  },
  {
    num: "03",
    icon: "✅",
    color: "#00A651",
    bg: "#f0fdf4",
    tag: "Problem Resolved",
    title: "المشكلة تُحلّ",
    desc: "The resident confirms the fix. The municipality's public record updates. Trust is built.",
  },
];
const FLOW = [
  { icon: "👤", label: "Citizen", sub: "Reports issue" },
  { arrow: true },
  { icon: "⚙️", label: "CivicFix", sub: "Routes it" },
  { arrow: true },
  { icon: "🏛️", label: "Baladiye", sub: "Resolves it" },
  { arrow: true },
  { icon: "✅", label: "Verified", sub: "By resident" },
];

function WelcomePage() {
  const navigate = useNavigate();
  // check if user is logged in — true if a token exists in localStorage
  const isLoggedIn = localStorage.getItem("token");

  // logout — clears everything and refreshes
  const handleLogout = () => {
    localStorage.removeItem("token"); // remove the token
    localStorage.removeItem("usr_Id"); // remove user id
    localStorage.removeItem("usr_FullName"); // remove name
    localStorage.removeItem("usr_Role"); // remove role
    navigate("/"); // go to home
    window.location.reload(); // refresh so the hole systems reruns so navbar updates
  };
  return (
    <div>
      {/* ── Navbar ── */}
      <nav className="navbar">
        <div className="navbar__brand">
          <div className="navbar__logo">🏙️</div>
          <span className="navbar__name">
            Civic<span>Fix</span>
          </span>
        </div>
        <div className="navbar__links">
          <a className="navbar__link">Features</a>
          <a className="navbar__link">How it works</a>
          <a className="navbar__link">My Reports</a>
          <a className="navbar__link">Dashboard</a>
          
          
        </div>
        <div className="navbar__buttons">
          {isLoggedIn ? (
            <button className="btn-red" onClick={handleLogout}>
              Logout / تسجيل الخروج
            </button>
          ) : (
            <>
              <button
                className="btn-outline"
                onClick={() => navigate("/login")}
              >
                Login
              </button>
              <button className="btn-red" onClick={() => navigate("/register")}>
                Register
              </button>
            </>
          )}
        </div>
      </nav>
      {/* ── Hero ── */}
      <section className="hero">
        {/* Left side — headline + description + buttons */}
        <div className="hero__left">
          {/* badge row — "Built for Lebanon" + Live dot */}
          <div className="hero__badge-row">
            <div className="hero__badge">
              🇱🇧 Built for Lebanon's Municipalities
            </div>
            <div className="hero__live">
              <div className="hero__live-dot" />
              Live
            </div>
          </div>

          {/* main headline — three separate lines */}
          <div className="hero__headlines">
            <h1 className="hero__headline">Your city</h1>
            <h1 className="hero__headline">deserves better.</h1>
            <h1 className="hero__headline hero__headline--red">
              Let's fix it.
            </h1>
          </div>

          {/* short red line below the headline */}
          <div className="hero__divider" />

          {/* description paragraph */}
          <p className="hero__description">
            CivicFix connects Lebanese residents directly to their baladiye.
            Report a problem, watch it get handled, and confirm when it's
            actually fixed.
          </p>

          {/* arabic subtitle */}
          <p className="hero__arabic">
            بلّغ عن مشاكل بلديتك — نحن نتكفّل بالباقي
          </p>

          {/* CTA buttons */}
          <div className="hero__cta">
            <button
              className="btn-hero-primary"
              onClick={() =>
                localStorage.getItem("token")
                  ? navigate("/Report")
                  : navigate("/register")
              }
            >
              🚨 Report a Problem
            </button>
            <button
              className="btn-hero-secondary"
              onClick={() => navigate("/dashboard")}
            >
              📊 View Dashboard
            </button>
          </div>

          {/* trust line */}
          <div className="hero__trust">
            <span>🗺️ 1,432 municipalities</span>
            <span className="hero__trust-divider">|</span>
            <span>🔒 Encrypted</span>
            <span className="hero__trust-divider">|</span>
            <span>✅ Free for residents</span>
          </div>
        </div>
        {/* Right column — stat cards grid */}
        <div className="hero__right">
          {/* card 1 — municipalities count */}
          <div className="stat-card">
            <div className="stat-card__label">Municipalities</div>
            <div className="stat-card__number">1,432</div>
            <div className="stat-card__sub">across all of Lebanon 🇱🇧</div>
            <div className="stat-card__bar stat-card__bar--red" />
          </div>

          {/* card 2 — average resolution time */}
          <div className="stat-card">
            <div className="stat-card__label">Avg. Resolution</div>
            <div className="stat-card__number stat-card__number--green">
              72h
            </div>
            <div className="stat-card__sub">from report to fix ⚡</div>
            <div className="stat-card__bar stat-card__bar--green" />
          </div>

          {/* card 3 — flow — spans both columns */}
          <div className="stat-card stat-card--wide">
            <div className="stat-card__label">How a report travels</div>
            <div className="flow">
              {FLOW.map(
                (
                  item,
                  i, // loop through every item in the FLOW array
                ) =>
                  // item = the current object, i = its index number (0,1,2...)
                  item.arrow ? ( // check: does this item have arrow:true ?
                    // YES — render just an arrow
                    <div key={i} className="flow__arrow">
                      →
                    </div>
                  ) : (
                    // NO — render a full step box
                    <div key={i} className="flow__step">
                      <div className="flow__icon-box">{item.icon}</div>{" "}
                      {/* emoji — e.g. 👤 */}
                      <div className="flow__label">{item.label}</div>{" "}
                      {/* name — e.g. "Citizen" */}
                      <div className="flow__sublabel">{item.sub}</div>{" "}
                      {/* subtitle — e.g. "Reports issue" */}
                    </div>
                  ),
              )}
            </div>
          </div>

          {/* card 4 — correctly routed */}
          <div className="stat-card">
            <div className="stat-card__label">Correctly Routed</div>
            <div className="stat-card__number stat-card__number--red">94%</div>
            <div className="stat-card__sub">auto GPS routing 📍</div>
            <div
              className="stat-card__bar stat-card__bar--red"
              style={{ width: "94%" }}
            />
          </div>

          {/* card 5 — coverage */}
          <div className="stat-card">
            <div className="stat-card__label">Coverage</div>
            <div
              style={{
                fontSize: "20px",
                fontWeight: 600,
                color: "#ffffff",
                marginBottom: "4px",
              }}
            >
              All Lebanon 🇱🇧
            </div>
            <div className="stat-card__sub">Every district, every village</div>
          </div>
        </div>
        {/* end hero__right */}
      </section>
      {/* ── Stats Strip ── */}
      <section className="stats-strip">
        <p className="stats-strip__overline">The scale of the problem</p>
        <h2 className="stats-strip__headline">Lebanon needs this. Now.</h2>
        <div className="stats-strip__grid">
          {STATS.map((stat) => (
            <div key={stat.num} className="stats-strip__card">
              <div className="stats-strip__icon">{stat.icon}</div>
              <div className="stats-strip__num">{stat.num}</div>
              <div className="stats-strip__name">{stat.label}</div>
              <div className="stats-strip__arabic">{stat.arabic}</div>
            </div>
          ))}
        </div>
      </section>
      {/* ── Steps Section ── */}
      <section className="steps">
        <p className="steps__overline">Simple by design</p>
        <h2 className="steps__headline">Three steps. Real results.</h2>
        <div className="steps__grid">
          {STEPS.map((step) => (
            <div key={step.num} className="step-card">
              <div className="step-card__top">
                <div
                  className="step-card__icon"
                  style={{ backgroundColor: step.bg }}
                >
                  {step.icon}
                </div>
                <span className="step-card__num" style={{ color: step.color }}>
                  {step.num}
                </span>
              </div>
              <div className="step-card__tag" style={{ color: step.color }}>
                {step.tag}
              </div>
              <h6 className="step-card__title">{step.title}</h6>
              <p className="step-card__desc">{step.desc}</p>
              <div
                className="step-card__bar"
                style={{ backgroundColor: step.color }}
              />
            </div>
          ))}
        </div>
      </section>
      {/* ── CTA Section ── */}
      <section className="cta">
        {" "}
        {/* green gradient section */}
        {/* leaf emoji above headline */}
        <div className="cta__icon">🌿</div>
        {/* arabic headline */}
        <h2 className="cta__headline-arabic">ساهم في بناء لبنان أفضل</h2>
        {/* english subtitle */}
        <p className="cta__headline-en">Build a better Lebanon.</p>
        {/* description */}
        <p className="cta__desc">
          Lebanon's infrastructure won't fix itself. Every report you submit is
          a step toward the country your children deserve.
        </p>
        {/* buttons row */}
        <div className="cta__buttons">
          {" "}
          {/* flex row — buttons side by side */}
          {/* primary button — white, navigates to /register */}
          <button
            className="btn-cta-primary"
            onClick={() =>
              localStorage.getItem("token")
                ? navigate("/Report")
                : navigate("/register")
            }
          >
            📋 ابدأ الآن / Get Started
          </button>
          {/* secondary button — ghost style, no route yet */}
          <button
            className="btn-cta-secondary"
            onClick={() => navigate("/dashboard")}
          >
            📊 View Dashboard
          </button>
        </div>{" "}
        {/* end cta__buttons */}
      </section>{" "}
      {/* end cta */}
      {/* ── Flag Strip ── */}
      <div className="flag-strip">
        {" "}
        {/* white bar — red line | cedar | green line */}
        <div className="flag-strip__red" />{" "}
        {/* red line on the left — empty div styled by CSS */}
        {/* cedar SVG — sits in the center between the two lines */}
        <svg
          width="36"
          height="36"
          viewBox="0 0 80 80"
          fill="none"
          style={{ zIndex: 2 }}
        >
          <rect x="36" y="60" width="8" height="15" rx="3" fill="#7B4A1E" />{" "}
          {/* trunk */}
          <ellipse cx="40" cy="56" rx="34" ry="6" fill="#00843D" />{" "}
          {/* bottom layer */}
          <ellipse cx="40" cy="44" rx="26" ry="6" fill="#009A47" />{" "}
          {/* middle layer */}
          <ellipse cx="40" cy="33" rx="18" ry="5.5" fill="#00B050" />{" "}
          {/* upper layer */}
          <ellipse cx="40" cy="23" rx="12" ry="5" fill="#00C45A" />{" "}
          {/* top layer */}
          <ellipse cx="40" cy="14" rx="7" ry="4.5" fill="#00D464" />{" "}
          {/* near top */}
          <ellipse cx="40" cy="7" rx="4" ry="3.5" fill="#00E070" /> {/* tip */}
        </svg>
        <div className="flag-strip__green" />{" "}
        {/* green line on the right — empty div styled by CSS */}
      </div>{" "}
      {/* end flag-strip */}
      {/* small text below the flag strip */}
      <p className="flag-strip__label">🇱🇧 Free for all Lebanese residents</p>
      {/* ── Footer ── */}
      <footer className="footer">
        {" "}
        {/* white footer */}
        {/* top row — brand on left, links on right */}
        <div className="footer__top">
          <div className="footer__brand">
            Civic<span>Fix</span> 🇱🇧
          </div>{" "}
          {/* "Fix" colored red via CSS */}
          <div className="footer__links">
            <a className="footer__link">Features</a>
            <a className="footer__link">How it works</a>
            <a className="footer__link">Privacy</a>
            <a className="footer__link">Contact</a>
          </div>
        </div>{" "}
        {/* end footer__top */}
        {/* bottom row — copyright on left, tagline on right */}
        <div className="footer__bottom">
          <span className="footer__copy">© 2026 CivicFix — Lebanon</span>
          <span className="footer__tagline">
            Digital infrastructure for Lebanon's municipalities
          </span>
        </div>{" "}
        {/* end footer__bottom */}
      </footer>{" "}
      {/* end footer */}
      {/* more sections coming here */}
    </div>
  );
}
export default WelcomePage;
