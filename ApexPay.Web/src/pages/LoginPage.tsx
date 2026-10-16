import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { api } from "../api/client.ts";
import type { LoginResponse } from "../api/types.ts";
import { saveSession } from "../auth/session.ts";
import { ErrorBanner, Brand } from "../components/ui.tsx";

export function LoginPage() {
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [sending, setSending] = useState(false);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!email.trim() || !password) {
      setError("Enter your email and password.");
      return;
    }
    setError("");
    setSending(true);
    try {
      const data = await api<LoginResponse>("/api/account/login", "POST", {
        email: email.trim(),
        password,
      });
      saveSession({ accessToken: data.accessToken, expiresAt: data.expiresAt });
      navigate("/");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Login failed.");
    } finally {
      setSending(false);
    }
  };

  return (
    <div className="page auth">
      <Brand tagline="Log in to your wallets." />
      {error && <ErrorBanner message={error} />}
      <div className="card">
        <form onSubmit={submit}>
          <label htmlFor="email">Email</label>
          <input id="email" type="email" autoComplete="email" value={email} onChange={(e) => setEmail(e.target.value)} />
          <label htmlFor="password">Password</label>
          <input
            id="password"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
          <button type="submit" disabled={sending}>
            {sending ? "Logging in…" : "Log in"}
          </button>
        </form>
      </div>
      <div className="auth-links">
        <p>
          <Link to="/signup">Create an account</Link>
        </p>
      </div>
    </div>
  );
}
