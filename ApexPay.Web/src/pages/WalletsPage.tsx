import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { api } from "../api/client.ts";
import { useWallets } from "../hooks/useWallets.ts";
import { ConfirmDialog, BottomNav, Empty, ErrorBanner, Loading, ScreenHeader, StatusPill } from "../components/ui.tsx";
import { formatMoney } from "../money.ts";

export function WalletsPage() {
  const navigate = useNavigate();
  const { wallets, loading, error, reload } = useWallets();
  const [name, setName] = useState("");
  const [creating, setCreating] = useState(false);
  const [createError, setCreateError] = useState("");
  const [closeTarget, setCloseTarget] = useState<{ id: string; name: string } | null>(null);
  const [closing, setClosing] = useState(false);
  const [closeError, setCloseError] = useState("");

  const create = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!name.trim()) return;
    setCreating(true);
    setCreateError("");
    try {
      await api("/api/wallet/create-wallet", "POST", { name: name.trim() });
      setName("");
      await reload();
    } catch (err) {
      setCreateError(err instanceof Error ? err.message : "Could not create wallet.");
    } finally {
      setCreating(false);
    }
  };

  const confirmClose = async () => {
    setClosing(true);
    setCloseError("");
    try {
      await api(`/api/wallet/${closeTarget!.id}/close-wallet`, "PATCH");
      setCloseTarget(null);
      await reload();
    } catch (err) {
      setCloseError(err instanceof Error ? err.message : "Could not close wallet.");
    } finally {
      setClosing(false);
    }
  };

  return (
    <div className="page with-bottom-nav">
      <ScreenHeader title="Wallets" />
      {error && <ErrorBanner message={error} onRetry={() => void reload()} />}
      {createError && <ErrorBanner message={createError} />}
      {loading && <Loading />}
      {!loading && wallets.length === 0 && <Empty text="No wallets yet — create one below." />}
      {!loading &&
        wallets.map((w) => (
          <div className="balance-card" key={w.id}>
            <div className="row">
              <span className="balance-name">{w.name}</span>
              <StatusPill label={w.status === "ACTIVE" ? "Active" : w.status} kind={w.status} />
            </div>
            <div className="balance-amount">{formatMoney(w.balance, w.currency)}</div>
            <div className="balance-meta mono">{w.accountNumber}</div>
            <div className="balance-actions">
              <button type="button" onClick={() => navigate("/transfer", { state: { fromWalletId: w.id } })}>
                Send
              </button>
              <button type="button" className="secondary" onClick={() => navigate(`/wallets/${w.id}`)}>
                Detail
              </button>
              {w.status === "ACTIVE" && w.balance === 0 && (
                <button
                  type="button"
                  className="danger"
                  onClick={() => {
                    setCloseError("");
                    setCloseTarget({ id: w.id, name: w.name });
                  }}
                >
                  Close
                </button>
              )}
            </div>
          </div>
        ))}
      <div className="card">
        <h2>Create wallet</h2>
        <form onSubmit={create}>
          <label htmlFor="name">Wallet name</label>
          <input id="name" value={name} onChange={(e) => setName(e.target.value)} maxLength={50} placeholder="e.g. Savings" />
          <button type="submit" disabled={creating || !name.trim()}>
            {creating ? "Creating…" : "Create wallet"}
          </button>
        </form>
      </div>
      <div className="quick-links">
        <Link to="/fund">Fund a wallet</Link>
        <Link to="/transfer">Send money</Link>
        <Link to="/checkout">Pay merchant</Link>
        <Link to="/refund">Refund</Link>
      </div>
      <ConfirmDialog
        open={closeTarget !== null}
        title="Close wallet"
        amount={closeTarget?.name ?? ""}
        details={[{ label: "Note", value: "This wallet has zero balance and will be closed permanently." }]}
        confirmLabel="Close wallet"
        sending={closing}
        error={closeError}
        onConfirm={() => void confirmClose()}
        onCancel={() => setCloseTarget(null)}
      />
      <BottomNav />
    </div>
  );
}
