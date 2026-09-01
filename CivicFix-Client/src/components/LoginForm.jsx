import { useState } from 'react'; 
import { useNavigate } from 'react-router-dom'; 
import '../styles/Login.css'; 
function LoginForm() {

  const navigate = useNavigate(); 
  const [formData, setFormData] = useState({
    Email: '',    
    Password: '', 
  });

  const [showPassword, setShowPassword] = useState(false); 
  const [error, setError] = useState('');  
  const [loading, setLoading] = useState(false);         
const handleChange = (e) => { // e = the event — info about what the user just typed
    setFormData({
      ...formData,                      // keep all existing field values
      [e.target.name]: e.target.value,  // update only the field the user typed in
    });
  };
const handleSubmit = async (e) => { // async = can wait for server without freezing the page
    e.preventDefault(); // stops browser from refreshing on submit
    setError('');        // clear any previous error
    setLoading(true);    // show "Signing in..." on the button

    try { // try to send the request — if something goes wrong jump to catch
      const response = await fetch('http://localhost:5140/api/Users/login', {
        method: 'POST',                                    // POST = sending data to server
        headers: { 'Content-Type': 'application/json' },  // tell server we're sending JSON
        body: JSON.stringify(formData),                    // convert formData to JSON string
      });

      if (response.ok) { // server said success
        const data = await response.json();                    // read response from backend
        localStorage.setItem('token', data.token);             // store the token
        localStorage.setItem('usr_Id', data.usr_Id);           // store the user id
        localStorage.setItem('usr_FullName', data.usr_FullName); // store the name
        localStorage.setItem('usr_Role', data.usr_Role);       // store the role
        navigate('/report');                                   // go to report page
      } else { // server said something went wrong
        const message = await response.text(); // read the error message from server
        setError(message || 'Invalid email or password.'); // show it on screen
      }

    } catch (err) { // couldn't reach the server at all
      setError('Could not connect to server. Please try again.');
    } finally { // runs no matter what — success or error
      setLoading(false); // turn off loading
    }
  };
return (
    <div className="login-page"> {/* full page — light gray background */}

      {/* white card — centered on the page */}
      <div className="login-card">

        {/* logo — clicking goes back to home */}
        <div className="login-card__logo" onClick={() => navigate('/')}>
          <div className="login-card__logo-box">🏙️</div> {/* red box with city emoji */}
          <span className="login-card__logo-name">Civic<span>Fix</span></span> {/* "Fix" colored red */}
        </div>

        {/* title */}
        <h2 className="login-card__title">Welcome back</h2>

        {/* "Don't have an account? Register" */}
        <p className="login-card__register">
          Don't have an account?{' '}
          <span className="login-card__register-link" onClick={() => navigate('/register')}>
            Register →
          </span>
        </p>

        {/* the form */}
        <form className="login-form" onSubmit={handleSubmit}>

          {/* email field */}
          <div className="form-group">
            <label className="form-label">Email address</label>
            <input
              className="form-input"
              type="email"
              name="Email"
              placeholder="you@example.com"
              value={formData.Email}
              onChange={handleChange}
              required
            />
          </div>

          {/* password field with show/hide toggle */}
          <div className="form-group">
            <label className="form-label">Password / كلمة المرور</label>
            <div className="form-input-wrapper">
              <input
                className="form-input form-input--password"
                type={showPassword ? 'text' : 'password'}
                name="Password"
                placeholder="Your password"
                value={formData.Password}
                onChange={handleChange}
                required
              />
              <button
                type="button"
                className="form-input__toggle"
                onClick={() => setShowPassword(!showPassword)}
              >
                {showPassword ? '🙈' : '👁️'}
              </button>
            </div>
          </div>

          {/* error message — only shows if error is not empty */}
          {error ? <div className="form-error">{error}</div> : null}

          {/* submit button */}
          <button
            className="btn-login"
            type="submit"
            disabled={loading}
          >
            {loading ? 'Signing in...' : 'Sign In / تسجيل الدخول'}
          </button>

        </form>

        {/* Lebanese flag strip */}
        <div className="login-card__flag">
          <div className="login-card__flag-red" />
          <svg width="24" height="24" viewBox="0 0 80 80" fill="none">
            <rect x="36" y="60" width="8" height="14" rx="3" fill="#7B4A1E" />
            <ellipse cx="40" cy="56" rx="34" ry="6" fill="#00843D" />
            <ellipse cx="40" cy="44" rx="26" ry="6" fill="#009A47" />
            <ellipse cx="40" cy="33" rx="18" ry="5.5" fill="#00B050" />
            <ellipse cx="40" cy="23" rx="12" ry="5" fill="#00C45A" />
            <ellipse cx="40" cy="14" rx="7" ry="4.5" fill="#00D464" />
            <ellipse cx="40" cy="7" rx="4" ry="3.5" fill="#00E070" />
          </svg>
          <div className="login-card__flag-green" />
        </div>

      </div> {/* end login-card */}

    </div> // end login-page
  );





}

export default LoginForm;