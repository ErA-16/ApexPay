import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { api } from "../api/client.ts";
import { ErrorBanner, OkBanner, Brand } from "../components/ui.tsx";

export function SignupPage() {
  const navigate = useNavigate();
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [done, setDone] = useState(false);
  const [sending, setSending] = useState(false);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!firstName.trim() || !lastName.trim() || !email.trim() || password.length < 8) {
      setError("Fill every field. Password needs at least 8 characters.");
      return;
    }
    setError("");
    setSending(true);
    try {
      await api("/api/account/signup", "POST", {
        firstName: firstName.trim(),
        lastName: lastName.trim(),
        email: email.trim(),
        password,
      });
      setDone(true);
      window.setTimeout(() => navigate("/login"), 1500);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Sign up failed.");
    } finally {
      setSending(false);
    }
  };

  return (
    <div className="page auth">
      <Brand tagline="Start using ApexPay in minutes." />
      {error && <ErrorBanner message={error} />}
      {done && <OkBanner message="Account created. Taking you to login…" />}
      <div className="card">
        <form onSubmit={submit}>
          <label htmlFor="firstName">First name</label>
          <input id="firstName" value={firstName} onChange={(e) => setFirstName(e.target.value)} maxLength={45} />
          <label htmlFor="lastName">Last name</label>
          <input id="lastName" value={lastName} onChange={(e) => setLastName(e.target.value)} maxLength={45} />
          <label htmlFor="email">Email</label>
          <input id="email" type="email" autoComplete="email" value={email} onChange={(e) => setEmail(e.target.value)} maxLength={255} />
          <label htmlFor="password">Password</label>
          <input
            id="password"
            type="password"
            autoComplete="new-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            maxLength={100}
            placeholder="At least 8 characters"
          />
          <button type="submit" disabled={sending}>
            {sending ? "Creating…" : "Create account"}
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
