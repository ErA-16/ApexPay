import { useCallback, useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { api } from "../api/client.ts";
import type { WalletHistoryResponse, WalletResponse } from "../api/types.ts";
import { ConfirmDialog, Empty, ErrorBanner, Loading, OkBanner, Pagination, StatusPill, TopNav } from "../components/ui.tsx";
import { formatMoney } from "../money.ts";

export function WalletDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [wallet, setWallet] = useState<WalletResponse | null>(null);
  const [history, setHistory] = useState<WalletHistoryResponse | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [newName, setNewName] = useState("");
  const [notice, setNotice] = useState("");
  const [working, setWorking] = useState(false);
  const [renameError, setRenameError] = useState("");
  const [closeOpen, setCloseOpen] = useState(false);
  const [closeError, setCloseError] = useState("");

  const load = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    setError("");
    try {
      const [w, h] = await Promise.all([
        api<WalletResponse>(`/api/wallet/${id}`, "GET"),
        api<WalletHistoryResponse>(`/api/wallet/${id}/history?page=${page}&limit=20`, "GET"),
      ]);
      setWallet(w);
      setHistory(h);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not load wallet.");
    } finally {
      setLoading(false);
    }
  }, [id, page]);

  useEffect(() => {
    void load();
  }, [load]);

  const rename = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!id || !newName.trim()) return;
    setWorking(true);
    setRenameError("");
    setNotice("");
    try {
      await api(`/api/wallet/${id}/rename`, "PATCH", { name: newName.trim() });
      setNewName("");
      setNotice("Wallet renamed.");
      await load();
    } catch (err) {
      setRenameError(err instanceof Error ? err.message : "Rename failed.");
    } finally {
      setWorking(false);
    }
  };

  const close = async () => {
    if (!id) return;
    setWorking(true);
    setCloseError("");
    try {
      await api(`/api/wallet/${id}/close-wallet`, "PATCH");
      setCloseOpen(false);
      navigate("/wallets");
    } catch (err) {
      setCloseError(err instanceof Error ? err.message : "Could not close wallet.");
    } finally {
      setWorking(false);
    }
  };

  return (
    <div className="page">
      <TopNav title="Wallet detail" />
      {error && <ErrorBanner message={error} onRetry={() => void load()} />}
      {notice && <OkBanner message={notice} />}
      {loading && <Loading />}
      {!loading && wallet && (
        <div className="hero-card">
          <div className="hero-top">
            <span className="hero-label">{wallet.name}</span>
            <StatusPill label={wallet.status === "ACTIVE" ? "Active" : wallet.status} kind={wallet.status} />
          </div>
          <div className="hero-amount">{formatMoney(wallet.balance, wallet.currency)}</div>
          <div className="hero-sub mono">{wallet.accountNumber}</div>
        </div>
      )}
      <div className="card">
        <h2>Rename</h2>
        {renameError && <ErrorBanner message={renameError} />}
        <form onSubmit={rename}>
          <label htmlFor="newName">New name</label>
          <input id="newName" value={newName} onChange={(e) => setNewName(e.target.value)} maxLength={50} />
          <button type="submit" disabled={working || !newName.trim()}>
            Rename
          </button>
        </form>
      </div>
      {wallet?.status === "ACTIVE" && (
        <div className="card">
          <h2>Close wallet</h2>
          <button
            type="button"
            className="danger"
            disabled={working || (wallet.balance !== 0)}
            onClick={() => {
              setCloseError("");
              setCloseOpen(true);
            }}
          >
            Close this wallet
          </button>
          {wallet.balance !== 0 && <p className="muted">Move the remaining balance out before closing.</p>}
        </div>
      )}
      <h2>History</h2>
      {history && history.entries.length === 0 && <Empty text="No transactions yet." />}
      {history?.entries.map((e) => (
        <div className="history-entry" key={e.reference}>
          <div className="row">
            <strong>{e.type}</strong>
            <StatusPill label={e.entryType} kind={e.entryType} />
          </div>
          <div className={`history-amount ${e.entryType.toLowerCase()}`}>
            {e.entryType === "CREDIT" ? "+" : "−"}
            {formatMoney(e.amount)}
          </div>
          <div className="muted mono" style={{ fontSize: 12 }}>
            {e.reference}
          </div>
          <div className="muted" style={{ fontSize: 12 }}>
            {new Date(e.timestamp).toLocaleString()}
          </div>
        </div>
      ))}
      {history && <Pagination page={history.page} totalPages={history.totalPages} onPage={setPage} />}
      {history && !history.isConsistent && (
        <div className="banner warn" role="alert">
          Stored balance differs from the ledger sum. Please contact support.
        </div>
      )}
      <ConfirmDialog
        open={closeOpen}
        title="Close wallet"
        amount={wallet?.name ?? ""}
        details={[{ label: "Note", value: "This wallet has zero balance and will be closed permanently." }]}
        confirmLabel="Close wallet"
        sending={working}
        error={closeError}
        onConfirm={() => void close()}
        onCancel={() => setCloseOpen(false)}
      />
    </div>
  );
}
