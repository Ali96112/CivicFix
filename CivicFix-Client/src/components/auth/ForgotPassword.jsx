import { useState } from "react";
import { useNavigate } from "react-router-dom";
import "../../styles/Login.css"; // reuse the login page styles

function ForgotPassword() {
  const navigate = useNavigate();

  const [email, setEmail] = useState("");        // the email the user types
  const [error, setError] = useState("");        // error message
  const [success, setSuccess] = useState("");    // success message
  const [loading, setLoading] = useState(false); // true while submitting

  // runs when the user submits their email
  const handleSubmit = async (e) => {
    e.preventDefault(); // stop the page from refreshing
    setError("");
    setSuccess("");
    setLoading(true);

    try {
      const response = await fetch("http://localhost:5140/api/Users/forgot-password", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ Email: email }), // send the email
      });

      if (response.ok) {
        setSuccess("Check your email for the reset link.");
      } else {
        const text = await response.text();
        setError(text || "Could not send reset email.");
      }
    } catch (err) {
      setError("Could not connect to server.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="login-page">
      <div className="login-card">

        <h1 className="login-card__title">Forgot Password</h1>
        <p className="login-card__register">
          Enter your email and we'll send you a reset link
        </p>

        <form className="login-form" onSubmit={handleSubmit}>

          <div className="form-group">
            <label className="form-label">Email address</label>
            <input
              className="form-input"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="you@example.com"
              required
            />
          </div>

          {error && <div className="form-error">{error}</div>}
          {success && <div className="form-success">{success}</div>}

          <button className="btn-login" type="submit" disabled={loading}>
            {loading ? "Sending..." : "Send Reset Link"}
          </button>

        </form>

        <p className="login-card__register" style={{ marginTop: "16px" }}>
          <span
            className="login-card__register-link"
            onClick={() => navigate("/login")}
          >
            ← Back to login
          </span>
        </p>

      </div>
    </div>
  );
}

export default ForgotPassword;