import Navbar from "../layout/Navbar";
import Footer from "../layout/Footer";
import "../../styles/info/Contact.css";

// one place to change the address — the card and the mailto both read from here
const EMAIL = "civicfix129@gmail.com";

function Contact() {
  return (
    <div className="contact-page">
      <Navbar />

      <div className="contact-page__content">
        <h1 className="contact-page__title">Contact</h1>
        <p className="contact-page__lead">
          Something broken in CivicFix itself, a question about your account, or a
          baladiye that wants to come on board — this is the inbox.
        </p>

        <a className="contact-card" href={"mailto:" + EMAIL + "?subject=CivicFix%20Support"}>
          <div className="contact-card__icon">
            <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#8ecdf5" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round"><rect x="3" y="5" width="18" height="14" rx="2.5"></rect><path d="M3.5 7l8.5 6 8.5-6"></path></svg>
          </div>

          <div className="contact-card__body">
            <span className="contact-card__label">Email</span>
            <span className="contact-card__value">{EMAIL}</span>
          </div>

          <svg className="contact-card__arrow" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M5 12h13"></path><path d="M13 6l6 6-6 6"></path></svg>
        </a>

        <h2 className="contact-page__heading">A problem in the street?</h2>
        <p className="contact-page__text">
          Don&apos;t email it — file it. A report sent by email reaches one person&apos;s
          inbox; a report filed in CivicFix reaches the baladiye that owns the street,
          gets a priority from the people who live there, and stays on the public
          record until someone closes it with a photo. This address is for CivicFix
          the system, not for potholes.
        </p>

        <h2 className="contact-page__heading">What to include</h2>
        <ul className="contact-list">
          <li>The report number, if your question is about a specific report.</li>
          <li>The email address on your CivicFix account.</li>
          <li>What you expected to happen, and what happened instead.</li>
          <li>A screenshot, if something on the screen looked wrong.</li>
        </ul>

        <h2 className="contact-page__heading">For municipalities</h2>
        <p className="contact-page__text">
          If you work for a baladiye and want your boundary and staff accounts set up
          on CivicFix, write to the same address and say which baladiye you represent.
        </p>
      </div>

      <Footer />
    </div>
  );
}

export default Contact;