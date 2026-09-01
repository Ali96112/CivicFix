import { useNavigate } from "react-router-dom";
import Navbar from "../layout/Navbar";
import Footer from "../layout/Footer";
import "../../styles/info/Privacy.css";

function Privacy() {
  const navigate = useNavigate();

  return (
    <div className="privacy-page">
      <Navbar />

      <div className="privacy-page__content">
        <h1 className="privacy-page__title">Privacy</h1>
        <p className="privacy-page__updated">Last updated — August 2026</p>

        <p className="privacy-page__lead">
          CivicFix is a public record of problems in Lebanese streets. That means some
          of what you write here is meant to be seen, and some of it is never shown to
          anyone. This page says exactly which is which — described from what the
          system actually does, not from a template.
        </p>

        <h2 className="privacy-page__heading">What we collect</h2>
        <p className="privacy-page__text">Three things, and nothing beyond them.</p>
        <ul className="privacy-list">
          <li>
            <strong>Your account.</strong> Your full name, your email address, your
            Lebanese mobile number, and your password — stored only as a hash, never in
            a form anyone can read back. If you are municipal staff, we also store which
            baladiye you work for.
          </li>
          <li>
            <strong>Your reports.</strong> A title, a description, a category, one
            photo, and the coordinates of the problem.
          </li>
          <li>
            <strong>Your activity on other people&apos;s reports.</strong> Your priority
            vote, whether you agreed the problem is real, and any comments you wrote.
          </li>
        </ul>
        <p className="privacy-page__text">
          We do not ask for a national ID number, a date of birth, or a home address.
          CivicFix used to have a national ID field and it was removed — a municipal
          reporting tool has no business holding one.
        </p>

        <h2 className="privacy-page__heading">What is public</h2>
        <p className="privacy-page__text">
          Every report is visible to anyone using CivicFix. That includes its title,
          description, photo, location pin, current status, and the tally of priority
          votes and agreements.
        </p>

        <div className="privacy-note">
          <strong>Your name appears on your report.</strong> The reports you file are
          shown with your full name, and so are your comments. Your email address and
          your phone number are never shown to anyone but you.
        </div>

        <h2 className="privacy-page__heading">Your photo</h2>
        <p className="privacy-page__text">
          The photo you attach is public. Street photos often catch more than the
          problem — faces, car plates, house numbers, the sign above a shop. Frame the
          pothole, not the people. If a photo shows someone who did not agree to be
          photographed, write to us and we will remove it.
        </p>

        <h2 className="privacy-page__heading">Your location</h2>
        <p className="privacy-page__text">
          The pin on a report marks where the problem is, not where you are. But if you
          report something outside your own building, those are the same place. Bear
          that in mind before reporting from your doorstep.
        </p>

        <h2 className="privacy-page__heading">Who can act on your report</h2>
        <p className="privacy-page__text">
          Staff at the baladiye whose boundary contains your pin can see your report and
          change its status. Administrators can see every report, move one to a different
          baladiye if it was routed to the wrong office, and delete reports that are
          abusive or fake.
        </p>

        <h2 className="privacy-page__heading">How long we keep things</h2>
        <p className="privacy-page__text">
          Resolved reports stay on the public record. That is deliberate — the point of
          the scoreboard is that a baladiye&apos;s history does not disappear once the
          street is fixed.
        </p>

        <div className="privacy-note">
          <strong>If an administrator blocks your account, every report you filed is
          deleted along with it</strong> — and the points those reports earned are taken
          back from the baladiyat that resolved them. Blocking is meant for accounts
          filing fake or abusive reports.
        </div>

        <h2 className="privacy-page__heading">How we protect your account</h2>
        <ul className="privacy-list">
          <li>Passwords are hashed. Nobody, including us, can read yours.</li>
          <li>A login lasts twelve hours, then you sign in again.</li>
          <li>
            A password-reset link expires, and stops working once it has been used.
          </li>
        </ul>

        <h2 className="privacy-page__heading">Cookies and browser storage</h2>
        <p className="privacy-page__text">
          CivicFix sets no tracking cookies. It does keep your login token in your
          browser&apos;s local storage so you are not asked to sign in on every page.
          Logging out removes it.
        </p>

        <h2 className="privacy-page__heading">What we don&apos;t do</h2>
        <ul className="privacy-list">
          <li>We do not sell or share your data with anyone.</li>
          <li>We do not show advertisements.</li>
          <li>We run no third-party analytics and no trackers.</li>
          <li>We do not follow you to other websites.</li>
        </ul>

        <h2 className="privacy-page__heading">Your rights</h2>
        <p className="privacy-page__text">
          Write to us and we will correct anything wrong in your account, remove a photo,
          or delete your account entirely. Reports you have already filed stay on the
          public record unless you ask for them to go too.
        </p>

        <h2 className="privacy-page__heading">Questions</h2>
        <p className="privacy-page__text">
          Everything on this page, including anything you disagree with, goes to the
          same place.
        </p>

        <button className="privacy-page__cta" onClick={() => navigate("/contact")}>
          Contact us
          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M5 12h13"></path><path d="M13 6l6 6-6 6"></path></svg>
        </button>
      </div>

      <Footer />
    </div>
  );
}

export default Privacy;