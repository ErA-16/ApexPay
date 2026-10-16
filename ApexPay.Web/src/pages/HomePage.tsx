import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { api } from "../api/client.ts";
import type { CreateAccountResponse, WalletResponse } from "../api/types.ts";
import { useWallets } from "../hooks/useWallets.ts";
import {
  AppHeader,
  BottomNav,
  ErrorBanner,
  IconFund,
  IconPay,
  IconRefund,
  IconSend,
  Loading,
  StatusPill,
} from "../components/ui.tsx";
import { formatMoney } from "../money.ts";

const quickActions = [
  { to: "/fund", label: "Fund", icon: IconFund },
  { to: "/transfer", label: "Send", icon: IconSend },
  { to: "/checkout", label: "Pay", icon: IconPay },
  { to: "/refund", label: "Refund", icon: IconRefund },
];

export function HomePage() {
  const { wallets, loading, error, reload } = useWallets();
  const [me, setMe] = useState<CreateAccountResponse | null>(null);
  const [nameError, setNameError] = useState("");
  const [hideBalance, setHideBalance] = useState(false);

  const loadMe = useCallback(async () => {
    setNameError("");
    try {
      const data = await api<CreateAccountResponse>("/api/account/me", "GET");
      setMe(data);
    } catch (err) {
      setNameError(err instanceof Error ? err.message : "Could not load your profile.");
    }
  }, []);

  useEffect(() => {
    void loadMe();
  }, [loadMe]);

  const firstName = me?.fullName?.trim().split(/\s+/)[0] ?? "";
  const total = wallets.reduce((sum, w) => sum + w.balance, 0);

  return (
    <div className="page with-bottom-nav">
      <AppHeader name={firstName || "there"} />
      {nameError && <ErrorBanner message={nameError} onRetry={() => void loadMe()} />}
      {error && <ErrorBanner message={error} onRetry={() => void reload()} />}

      <div className="hero-card">
        <div className="hero-top">
          <span className="hero-label">Total balance</span>
          <button
            type="button"
            className="hero-toggle"
            onClick={() => setHideBalance((v) => !v)}
            aria-pressed={hideBalance}
          >
            {hideBalance ? "Show" : "Hide"}
          </button>
        </div>
        <div className="hero-amount">{hideBalance ? "••••••" : formatMoney(total)}</div>
        <div className="hero-sub">
          {wallets.length} {wallets.length === 1 ? "wallet" : "wallets"}
        </div>
      </div>

      <div className="quick-actions">
        {quickActions.map((action) => (
          <Link key={action.to} to={action.to} className="quick-action">
            <span className="icon">
              <action.icon size={22} />
            </span>
            {action.label}
          </Link>
        ))}
      </div>

      <div className="section-head">
        <h2>Your wallets</h2>
        <Link to="/wallets">See all</Link>
      </div>

      {loading && <Loading />}
      {!loading && wallets.length === 0 && (
        <div className="empty-state">
          <div className="empty-title">No wallets yet</div>
          <p className="muted">Create your first wallet to start moving money.</p>
          <Link to="/wallets">Create a wallet</Link>
        </div>
      )}
      {!loading &&
        wallets.map((w: WalletResponse) => (
          <Link key={w.id} to={`/wallets/${w.id}`} className="wallet-card">
            <div className="row">
              <span className="balance-name">{w.name}</span>
              <StatusPill label={w.status === "ACTIVE" ? "Active" : w.status} kind={w.status} />
            </div>
            <div className="wallet-amount">
              {hideBalance ? "••••••" : formatMoney(w.balance, w.currency)}
            </div>
            <div className="wallet-meta mono">{w.accountNumber}</div>
          </Link>
        ))}

      <BottomNav />
    </div>
  );
}
