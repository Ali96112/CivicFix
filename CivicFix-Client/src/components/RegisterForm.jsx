import { useState } from "react"; // useState lets us store and update form field values
import { useNavigate } from "react-router-dom"; // useNavigate lets us switch pages after registration
import "../styles/Register.css"; // styles for this page

function RegisterForm() {
  const navigate = useNavigate(); // to go to login page after registration

  const [formData, setFormData] = useState({
    usr_FullName: "", // full name field
    usr_Email: "", // email field
    usr_PasswordHash: "", // password field
    usr_NationalId: "", // national ID field
  });

  const [showPassword, setShowPassword] = useState(false); // password visible or hidden
  const [error, setError] = useState(""); // error message shown to user
  const [loading, setLoading] = useState(false); // true while waiting for server

  const handleChange = (e) => {
    // e = the event — info about what the user just typed
    setFormData({
      ...formData, // keep all existing field values
      [e.target.name]: e.target.value, // update only the field the user typed in
    });
  };

  const handleSubmit = async (e) => {
    // async = can wait for server without freezing the page
    e.preventDefault(); // stops the browser from refreshing the page when form is submitted
    setError(""); // clear any previous error
    setLoading(true); // show "Creating account..." on the button

    try {
      // try to send the request — if something goes wrong jump to catch
      const response = await fetch("http://localhost:5140/api/Users/register", {
        method: "POST", // POST = sending data to the server
        headers: { "Content-Type": "application/json" }, // tell server we're sending JSON
        body: JSON.stringify(formData), // convert formData object to JSON string
      });

      if (response.ok) {
        // localStorage — a built-in browser storage that keeps data
        const data = await response.json(); // read the response object from backend
        localStorage.setItem("token", data.token); // store the token
        localStorage.setItem("usr_Id", data.usr_Id); // store the user id
        localStorage.setItem("usr_FullName", data.usr_FullName); // store the name
        localStorage.setItem("usr_Role", data.usr_Role); // store the role
        navigate("/report"); // go to report page
      } else {
        // server said something went wrong
        const message = await response.text(); // read the error message from server
        setError(message || "Registration failed. Please try again."); // show it on screen
      }
    } catch (err) {
      // couldn't reach the server at all
      setError("Could not connect to server. Please try again.");
    } finally {
      // runs no matter what — success or error
      setLoading(false); // turn off loading
    }
  };

  return (
    <div className="register-page">
      <div className="register-card">
        {/* logo — clicking goes back to home */}
        <div className="register-card__logo" onClick={() => navigate("/")}>
          <div className="register-card__logo-box">🏙️</div>{" "}
          {/* red box with city emoji */}
          <span className="register-card__logo-name">
            Civic<span>Fix</span>
          </span>{" "}
          {/* "Fix" colored red by CSS */}
        </div>

        {/* title */}
        <h2 className="register-card__title">Create account</h2>
        {/* "Already have an account? Sign in" */}
        <p className="register-card__signin">
          Already have an account?{" "}
          <span
            className="register-card__signin-link"
            onClick={() => navigate("/login")}
          >
            Sign in →
          </span>
        </p>

        {/* the form — calls handleSubmit when submitted */}
        <form className="register-form" onSubmit={handleSubmit}>
          {/* full name field */}
          <div className="form-group">
            {" "}
            {/* wraps label and input together */}
            <label className="form-label">Full Name / الاسم الكامل</label>
            <input
              className="form-input"
              type="text" // regular text input
              name="usr_FullName" // matches the key in formData
              placeholder="e.g. Adam Kadiri"
              value={formData.usr_FullName} //Input, display whatever is currently stored in formData.usr_FullName here it show it in this inpute field
              onChange={handleChange} // updates formData on every keystroke
              required //browser won't submit if empty
            />
            {/*so here user type adam kadiri the function on change run , e.target, set name value, setform data function run ,then saved in formData.usr_FullName */}
          </div>
          {/* email field */}
          <div className="form-group">
            <label className="form-label">Email address</label>
            <input
              className="form-input"
              type="email"
              name="usr_Email"
              placeholder="you@example.com"
              value={formData.usr_Email}
              onChange={handleChange}
              required
            />
          </div>
          {/* password field with show/hide toggle */}
          <div className="form-group">
            <label className="form-label">Password / كلمة المرور</label>
            <div className="form-input-wrapper">
              {" "}
              {/* wraps input + toggle button together */}
              <input
                className="form-input form-input--password"
                type={showPassword ? "text" : "password"} //if false give type is text and if true it well give type equal password so hidden by defasult false
                name="usr_PasswordHash"
                placeholder="Min. 8 characters"
                value={formData.usr_PasswordHash}
                onChange={handleChange}
                required
              />
              {/* show/hide toggle button */}
              <button
                type="button"
                className="form-input__toggle"
                onClick={() => setShowPassword(!showPassword)} //this is the eyebutton is give reverse if hidden and pressed show for thst it id !showP
              >
                {showPassword ? "🙈" : "👁️"}
                {/*Click 👁️ ,then setShowPassword() function run, reverse it, change type of type*/}
              </button>
            </div>
          </div>
          {/* national ID field */}
          <div className="form-group">
            <label className="form-label">National ID / الرقم الوطني</label>
            <input
              className="form-input"
              type="text"
              name="usr_NationalId"
              placeholder="Your Lebanese national ID number"
              value={formData.usr_NationalId}
              onChange={handleChange}
              required
            />
          </div>
          {/* error message — only shows if error state is not empty */}
          {error ? <div className="form-error">{error}</div> : null}{" "}
          {/*is erroe=true show the error while false null */}
          {/* submit button */}
          <button
            className="btn-register" //* styles from Register.css — solid red button */
            type="submit" //* clicking this submits the form — calls handleSubmit */}
            disabled={loading} //* when loading is true — button is disabled, can't click twice */}
          >
            {loading ? "Creating account..." : "Create Account / إنشاء حساب"}
            {/* if loading is true → show "Creating account..." */}
            {/* if loading is false → show "Create Account / إنشاء حساب" */}
          </button>
        </form>

{/* Lebanese flag strip — red line | cedar | green line */}
      <div className="register-card__flag">
        <div className="register-card__flag-red" /> {/* red line on the left */}
        {/* cedar SVG in the center */}
        <svg width="24" height="24" viewBox="0 0 80 80" fill="none">
          <rect x="36" y="60" width="8" height="14" rx="3" fill="#7B4A1E" />
          <ellipse cx="40" cy="56" rx="34" ry="6" fill="#00843D" />
          <ellipse cx="40" cy="44" rx="26" ry="6" fill="#009A47" />
          <ellipse cx="40" cy="33" rx="18" ry="5.5" fill="#00B050" />
          <ellipse cx="40" cy="23" rx="12" ry="5" fill="#00C45A" />
          <ellipse cx="40" cy="14" rx="7" ry="4.5" fill="#00D464" />
          <ellipse cx="40" cy="7" rx="4" ry="3.5" fill="#00E070" />
        </svg>
        <div className="register-card__flag-green" />{" "}
        {/* green line on the right */}
      </div>


      </div>

      
    </div>
  );
}

export default RegisterForm;
