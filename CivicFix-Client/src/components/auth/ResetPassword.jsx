import { useState } from "react";
import { useSearchParams, useNavigate } from "react-router-dom";
import "../../styles/Login.css"; // reuse the login page styles

function ResetPassword() {
  const navigate = useNavigate();

  // read the ?token=... value out of the URL (the email link put it there)
  const [searchParams] = useSearchParams();
  const token = searchParams.get("token");

  const [newPassword, setNewPassword] = useState(""); // the new password
  const [error, setError] = useState("");             // error message
  const [success, setSuccess] = useState("");         // success message
  const [loading, setLoading] = useState(false);      // true while submitting

  // runs when the user submits the new password
  const handleSubmit = async (e) => {
    e.preventDefault(); // stop the page from refreshing
    setError("");
    setSuccess("");
    setLoading(true);

    try {
      const response = await fetch("http://localhost:5140/api/Users/reset-password", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          Token: token,             // the token from the URL
          NewPassword: newPassword, // the new password the user typed
        }),
      });

      if (response.ok) {
        setSuccess("Password reset! Redirecting to login...");
        setTimeout(() => navigate("/login"), 1500); // go to login after 1.5s
      } else {
        const text = await response.text();
        setError(text || "Could not reset password.");
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

        <h1 className="login-card__title">Reset Password</h1>
        <p className="login-card__register">Enter your new password below</p>

        <form className="login-form" onSubmit={handleSubmit}>

          <div className="form-group">
            <label className="form-label">New Password</label>
            <input
              className="form-input"
              type="password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              placeholder="Enter new password"
              required
            />
          </div>

          {error && <div className="form-error">{error}</div>}
          {success && <div className="form-success">{success}</div>}

          <button className="btn-login" type="submit" disabled={loading}>
            {loading ? "Resetting..." : "Reset Password"}
          </button>

        </form>
      </div>
    </div>
  );
}

export default ResetPassword;