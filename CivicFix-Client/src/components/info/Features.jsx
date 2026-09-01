import Navbar from "../layout/Navbar";
import Footer from "../layout/Footer";
import "../../styles/info/Features.css";

// the landing page shows three of these — this page shows all five,
// ordered the way a report actually moves through the system
const ALL_FEATURES = [
  {
    icon: (
      <>
        <path d="M12 21s7-5.6 7-11a7 7 0 1 0-14 0c0 5.4 7 11 7 11z" />
        <circle cx="12" cy="10" r="2.4" />
      </>
    ),
    title: "Automatic routing",
    desc: "Drop a pin and CivicFix finds which baladiye's boundary contains it. No dropdown, no guessing which office is yours.",
    how: "Every baladiye is stored as a real polygon on the map of Lebanon. Your coordinates are tested against all of them at once, so the answer takes one query.",
  },
  {
    icon: (
      <>
        <rect x="3" y="5" width="18" height="14" rx="2.5" />
        <path d="M12 5v14" />
        <circle cx="12" cy="12" r="2.4" />
      </>
    ),
    title: "Border problems get an owner",
    desc: "A pothole on the line between two baladiyat is assigned to both, then handed to one. So it isn't everyone's job and therefore nobody's.",
    how: "Until an admin decides, the report is hidden from both — so neither assumes the other is handling it.",
  },
  {
    icon: (
      <>
        <path d="M6 19v-5" />
        <path d="M12 19V9" />
        <path d="M18 19V6" />
      </>
    ),
    title: "Residents set the priority",
    desc: "Neighbours vote High, Medium or Low and confirm the problem is real. The baladiye sees what the street actually cares about.",
    how: "One vote each, changeable at any time. The tally is public on every report page.",
  },
  {
    icon: (
      <>
        <rect x="3" y="7" width="18" height="13" rx="2.5" />
        <circle cx="12" cy="13.5" r="3.4" />
        <path d="M8.4 7l1.4-3h4.4l1.4 3" />
      </>
    ),
    title: "Proof, not promises",
    desc: "A report cannot be marked Resolved without an 'after' photo. Closing a ticket is not the same as fixing a street.",
    how: "The API rejects a Resolved status that arrives without a photo — it is a rule, not a reminder.",
  },
  {
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
    how: "Ten points for a resolved report. One point lost per day for anything left open past a week.",
  },
];

function Features() {
  return (
    <div className="features-page">
      <Navbar />

      <div className="features-page__content">
        <h1 className="features-page__title">What CivicFix does</h1>
        <p className="features-page__lead">
          Five things, and every one of them is built — not a roadmap. They are listed
          in the order a report meets them, from the moment it is filed to the moment
          the baladiye's record updates.
        </p>

        <div className="features-page__list">
          {ALL_FEATURES.map((feature, index) => (
            <div key={feature.title} className="feature-row">
              <div className="feature-row__icon">
                <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#8ecdf5" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round">
                  {feature.icon}
                </svg>
              </div>

              <div className="feature-row__body">
                <span className="feature-row__num">{String(index + 1).padStart(2, "0")}</span>
                <h2 className="feature-row__title">{feature.title}</h2>
                <p className="feature-row__desc">{feature.desc}</p>
                <p className="feature-row__how">{feature.how}</p>
              </div>
            </div>
          ))}
        </div>
      </div>

      <Footer />
    </div>
  );
}

export default Features;