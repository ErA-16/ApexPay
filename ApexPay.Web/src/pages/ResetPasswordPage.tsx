import { useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { api } from "../api/client.ts";
import { ErrorBanner, OkBanner, Brand } from "../components/ui.tsx";

export function ResetPasswordPage() {
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const token = params.get("token") ?? "";
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState(token ? "" : "This reset link is incomplete. Request a new link.");
  const [done, setDone] = useState(false);
  const [sending, setSending] = useState(false);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!token) {
      return;
    }
    if (password.length < 8) {
      setError("Choose a password of at least 8 characters.");
      return;
    }
    if (password !== confirmPassword) {
      setError("Passwords do not match.");
      return;
    }
    setError("");
    setSending(true);
    try {
      await api("/api/account/reset-password", "POST", { token, newPassword: password });
      setDone(true);
      window.setTimeout(() => navigate("/login"), 1500);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Reset failed.");
    } finally {
      setSending(false);
    }
  };

  return (
    <div className="page auth">
      <Brand tagline="Choose a new password." />
      {error && <ErrorBanner message={error} />}
      {done && <OkBanner message="Password reset. Taking you to login…" />}
      <div className="card">
        <form onSubmit={submit}>
          <label htmlFor="password">New password</label>
          <input
            id="password"
            type="password"
            autoComplete="new-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            maxLength={100}
            placeholder="At least 8 characters"
          />
          <label htmlFor="confirmPassword">Confirm new password</label>
          <input
            id="confirmPassword"
            type="password"
            autoComplete="new-password"
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            maxLength={100}
            placeholder="Re-enter your password"
          />
          <button type="submit" disabled={sending || !token}>
            {sending ? "Resetting…" : "Reset password"}
          </button>
        </form>
      </div>
      <div className="auth-links">
        <p>
          <Link to="/login">Back to login</Link>
        </p>
      </div>
    </div>
  );
}
