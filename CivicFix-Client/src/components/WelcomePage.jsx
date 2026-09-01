import { useNavigate } from "react-router-dom"; // useNavigate is a function that lets us navigate between pages /login, /register
import "../styles/Welcome.css";
import Navbar from "./layout/Navbar";
import Footer from "./layout/Footer";
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
    color: "#3d8fd4",
    icon: (
      <>
        <rect x="3" y="7" width="18" height="13" rx="2.5" />
        <circle cx="12" cy="13.5" r="3.4" />
        <path d="M8.4 7l1.4-3h4.4l1.4 3" />
      </>
    ),
    tag: "Citizen Reports",
    title: "المواطن يبلّغ",
    desc: "A resident submits an issue — photo, location, description. CivicFix finds their municipality automatically.",
  },
  {
    num: "02",
    color: "#3d8fd4",
    icon: (
      <>
        <path d="M4 20V9.5L12 4l8 5.5V20" />
        <path d="M9.2 20v-6h5.6v6" />
        <path d="M3 20h18" />
      </>
    ),
    tag: "Baladiye Receives",
    title: "البلدية تستلم",
    desc: "The report arrives in the municipality's dashboard — categorized, prioritized, and ready to assign to a team.",
  },
  {
    num: "03",
    color: "#4aa85a",
    icon: (
      <>
        <circle cx="12" cy="12" r="8.6" />
        <path d="M8.4 12.4l2.5 2.5 4.7-5.2" />
      </>
    ),
    tag: "Problem Resolved",
    title: "المشكلة تُحلّ",
    desc: "The resident confirms the fix. The municipality's public record updates. Trust is built.",
  },
];
// Every tile here is something the backend actually does — no promises.
const FEATURES = [
  {
    // a map pin — drawn, not an emoji, so it scales and takes the brand colour
    icon: (
      <>
        <path d="M12 21s7-5.6 7-11a7 7 0 1 0-14 0c0 5.4 7 11 7 11z" />
        <circle cx="12" cy="10" r="2.4" />
      </>
    ),
    title: "Automatic routing",
    desc: "Drop a pin and CivicFix finds which baladiye's boundary contains it. No dropdown, no guessing which office is yours.",
    how: "Every baladiye is stored as a real polygon — the answer takes one query.",
  },
  {
    // three rising bars — priority
    icon: (
      <>
        <path d="M6 19v-5" />
        <path d="M12 19V9" />
        <path d="M18 19V6" />
      </>
    ),
    title: "Residents set the priority",
    desc: "Neighbours vote High, Medium or Low and confirm the problem is real. The baladiye sees what the street actually cares about.",
    how: "One vote each, changeable. The tally is public on every report.",
  },
  {
    // a trophy
    icon: (
      <>
        <path d="M7.5 4h9v5.2a4.5 4.5 0 0 1-9 0V4z" />
        <path d="M7.5 6H4.6v.9a3 3 0 0 0 2.9 3" />
        <path d="M16.5 6h2.9v.9a3 3 0 0 1-2.9 3" />
        <path d="M12 13.7V17" />
        <path d="M9 20h6" />
      </>
    ),
    title: "Public scoreboard",
    desc: "Every baladiye earns points for resolving reports, and loses them for letting reports go stale. The ranking is public.",
    how: "Ten points for a fix. One lost per day past a week.",
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
  // "Report a Problem" appears in three places (hero, CTA, footer) and behaves the
  // same in all of them: logged in → the reports page, logged out → register first.
  // One helper so the three can never drift apart.
  const goToReport = () => {
    if (localStorage.getItem("token")) {
      navigate("/report");
    } else {
      navigate("/register");
    }
  };

  return (
    <div>
      <Navbar />
       
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
            <button className="btn-hero-primary" onClick={goToReport}>
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
            {/* ── Features Section ── */}
      {/* Sits ABOVE the steps so the navbar links, read left to right, walk you
          down the page in the same order. */}
      <section className="features" id="features">
        <p className="steps__overline">What CivicFix does</p>
        <h2 className="steps__headline">Built to actually get things fixed.</h2>
        <p className="features__sub">
          Three of the things that make a report land somewhere instead of disappearing.
        </p>

        <div className="features__grid">
          {FEATURES.map((feature) => (
            <div key={feature.title} className="feature-tile">
              <div className="feature-tile__icon">
                <svg
                  width="22"
                  height="22"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="#3d8fd4"
                  strokeWidth="1.75"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                >
                  {feature.icon}
                </svg>
              </div>

              <h6 className="feature-tile__title">{feature.title}</h6>
              <p className="feature-tile__desc">{feature.desc}</p>
              <p className="feature-tile__how">{feature.how}</p>
            </div>
          ))}
        </div>

      
      </section>

            {/* ── Steps Section ── */}
      <section className="journey" id="how">
        <p className="steps__overline">Simple by design</p>
        <h2 className="steps__headline">Three steps. Real results.</h2>

        {/* the rail — nodes joined by lines, so the eye reads 01 → 02 → 03 */}
        <div className="journey__rail">
          <div className="journey__node">01</div>
          <div className="journey__line" />
          <div className="journey__node">02</div>
          <div className="journey__line journey__line--end" />
          <div className="journey__node journey__node--done">03</div>
        </div>

        <div className="journey__grid">
          {STEPS.map((step) => (
            <div key={step.num} className="journey__step">
              <div className="journey__icon">
                <svg
                  width="24"
                  height="24"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke={step.color}
                  strokeWidth="1.6"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                >
                  {step.icon}
                </svg>
              </div>
              <div className="journey__tag" style={{ color: step.color }}>
                {step.tag}
              </div>
              <h6 className="journey__title">{step.title}</h6>
              <p className="journey__desc">{step.desc}</p>
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
          <button className="btn-cta-primary" onClick={goToReport}>
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
       <Footer />
    </div>
  );
}
export default WelcomePage;
