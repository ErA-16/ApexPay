import { useState } from "react";
import { Link } from "react-router-dom";
import { api } from "../api/client.ts";
import { ErrorBanner, OkBanner, Brand } from "../components/ui.tsx";

export function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [error, setError] = useState("");
  const [sent, setSent] = useState(false);
  const [sending, setSending] = useState(false);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!email.trim()) {
      setError("Enter your email.");
      return;
    }
    setError("");
    setSending(true);
    try {
      await api("/api/account/forgot-password", "POST", { email: email.trim() });
      setSent(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Request failed.");
    } finally {
      setSending(false);
    }
  };

  return (
    <div className="page auth">
      <Brand tagline="We'll email you a reset link." />
      {error && <ErrorBanner message={error} />}
      {sent && (
        <OkBanner message="If an account with that email exists, a reset link has been sent. Check your inbox and spam folder." />
      )}
      <div className="card">
        <form onSubmit={submit}>
          <label htmlFor="email">Email</label>
          <input id="email" type="email" autoComplete="email" value={email} onChange={(e) => setEmail(e.target.value)} />
          <button type="submit" disabled={sending}>
            {sending ? "Sending…" : "Send reset link"}
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
