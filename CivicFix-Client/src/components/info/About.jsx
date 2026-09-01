import Navbar from "../layout/Navbar";
import Footer from "../layout/Footer";
import "../../styles/info/About.css";

function About() {
  return (
    <div className="about-page">
      <Navbar />

      <div className="about-content">
        <h1 className="about-title">About CivicFix</h1>
        <p className="about-lead">
          Lebanon has over 1,000 municipalities and no shared way for residents to
          report the problems on their own streets. CivicFix is that channel — a
          pothole, a broken streetlight, uncollected rubbish, reported in a minute
          and routed to whoever is actually responsible for it.
        </p>

        <h2 className="about-heading">Why it exists</h2>
        <p className="about-text">
          Most people who want to report a problem give up at the first question:
          which baladiye do I even call? CivicFix answers that from your GPS
          coordinates, so nobody has to know where one municipality ends and the
          next begins.
        </p>
        <p className="about-text">
          And because every resolved report earns the baladiye public points, the
          work is visible. A resident can see whether their municipality is fixing
          things or letting them sit.
        </p>

        <h2 className="about-heading">How it works</h2>

        <div className="about-step">
          <span className="about-step__num">STEP 01</span>
          <h3 className="about-step__title">📍 The resident reports</h3>
          <p className="about-step__text">
            Take a photo, drop a pin on the map, describe the problem. CivicFix works
            out which baladiye that point falls inside — automatically. If the same
            issue was already reported nearby, you are sent to it instead, so one
            pothole stays one report.
          </p>
        </div>

        <div className="about-step">
          <span className="about-step__num">STEP 02</span>
          <h3 className="about-step__title">🏛️ The baladiye receives it</h3>
          <p className="about-step__text">
            The report appears in that baladiye&apos;s list with its category and the
            priority neighbours voted on. If the spot sits on a border between two
            baladiyat, an admin decides which one owns it — so it is never
            everyone&apos;s job and therefore nobody&apos;s.
          </p>
        </div>

        <div className="about-step">
          <span className="about-step__num">STEP 03</span>
          <h3 className="about-step__title">✅ The problem is resolved</h3>
          <p className="about-step__text">
            The baladiye fixes it and uploads an &ldquo;after&rdquo; photo — a report cannot be
            closed without one. Points go to the baladiye that did the work, and the
            public leaderboard updates.
          </p>
        </div>
      </div>

      <Footer />
    </div>
  );
}

export default About;