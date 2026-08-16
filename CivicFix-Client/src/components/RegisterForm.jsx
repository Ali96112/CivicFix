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

  const handleChange = (e) => { // e = the event — info about what the user just typed
    setFormData({
      ...formData, // keep all existing field values
      [e.target.name]: e.target.value, // update only the field the user typed in
    });
  };

  const handleSubmit = async (e) => { // async = can wait for server without freezing the page
    e.preventDefault(); // stops the browser from refreshing the page when form is submitted
    setError(""); // clear any previous error
    setLoading(true); // show "Creating account..." on the button

    try { // try to send the request — if something goes wrong jump to catch
      const response = await fetch("http://localhost:5000/api/Users/register", {
        method: "POST", // POST = sending data to the server
        headers: { "Content-Type": "application/json" }, // tell server we're sending JSON
        body: JSON.stringify(formData), // convert formData object to JSON string
      });

      if (response.ok) { // localStorage — a built-in browser storage that keeps data
        const data = await response.json(); // read the response object from backend
        localStorage.setItem("token", data.token); // store the token
        localStorage.setItem("usr_Id", data.usr_Id); // store the user id
        localStorage.setItem("usr_FullName", data.usr_FullName); // store the name
        localStorage.setItem("usr_Role", data.usr_Role); // store the role
        navigate("/report"); // go to report page
      } else { // server said something went wrong
        const message = await response.text(); // read the error message from server
        setError(message || "Registration failed. Please try again."); // show it on screen
      }
    } catch (err) { // couldn't reach the server at all
      setError("Could not connect to server. Please try again.");
    } finally { // runs no matter what — success or error
      setLoading(false); // turn off loading
    }
  };

  return (
    <div className="register-page">
      <div className="register-card">
{/* logo — clicking goes back to home */}
        <div className="register-card__logo" onClick={() => navigate('/')}>
          <div className="register-card__logo-box">🏙️</div> {/* red box with city emoji */}
          <span className="register-card__logo-name">Civic<span>Fix</span></span> {/* "Fix" colored red by CSS */}
        </div>

        {/* title */}
        <h2 className="register-card__title">Create account</h2>
        {/* "Already have an account? Sign in" */}
        <p className="register-card__signin">
          Already have an account?{' '}
          <span className="register-card__signin-link" onClick={() => navigate('/login')}>
            Sign in →
          </span>
        </p>

        {/* the form — calls handleSubmit when submitted */}
        <form className="register-form" onSubmit={handleSubmit}>
          {/* full name field */}
          <div className="form-group"> {/* wraps label and input together */}
            <label className="form-label">Full Name / الاسم الكامل</label>
            <input
              className="form-input"
              type="text"                        // regular text input 
              name="usr_FullName"                // matches the key in formData 
              placeholder="e.g. Adam Kadiri"
              value={formData.usr_FullName}      //controlled — value comes from state 
              onChange={handleChange}             // updates formData on every keystroke 
              required                          //browser won't submit if empty 
            />
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
            <div className="form-input-wrapper"> {/* wraps input + toggle button together */}
              <input
                className="form-input form-input--password"
                type={showPassword ? 'text' : 'password'}
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
                onClick={() => setShowPassword(!showPassword)}
              >
                {showPassword ? '🙈' : '👁️'}
              </button>
            </div>
          </div>
        </form>


      </div>
    </div>
  );

}

export default RegisterForm;