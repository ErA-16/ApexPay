import { useCallback, useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { api } from "../api/client.ts";
import type { AccountHistoryResponse } from "../api/types.ts";
import { BottomNav, Empty, ErrorBanner, Loading, Pagination, ScreenHeader, StatusPill } from "../components/ui.tsx";
import { formatMoney } from "../money.ts";

export function HistoryPage() {
  const [params, setParams] = useSearchParams();
  const walletFilter = params.get("walletId") ?? "";
  const page = Math.max(1, Number(params.get("page") ?? "1") || 1);
  const [data, setData] = useState<AccountHistoryResponse | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const query = new URLSearchParams({ page: String(page), limit: "20" });
      if (walletFilter) {
        query.set("walletId", walletFilter);
      }
      const result = await api<AccountHistoryResponse>(`/api/wallet/transactions?${query.toString()}`, "GET");
      setData(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not load history.");
    } finally {
      setLoading(false);
    }
  }, [page, walletFilter]);

  useEffect(() => {
    void load();
  }, [load]);

  const changeWallet = (value: string) => {
    const next = new URLSearchParams();
    if (value) {
      next.set("walletId", value);
    }
    setParams(next);
  };

  const changePage = (p: number) => {
    const next = new URLSearchParams(params);
    next.set("page", String(p));
    setParams(next);
  };

  return (
    <div className="page with-bottom-nav">
      <ScreenHeader title="History" />
      {error && <ErrorBanner message={error} onRetry={() => void load()} />}

      <div className="card">
        <label htmlFor="history-wallet-filter">Wallet</label>
        <select id="history-wallet-filter" value={walletFilter} onChange={(e) => changeWallet(e.target.value)}>
          <option value="">All wallets</option>
          {data?.wallets.map((w) => (
            <option key={w.id} value={w.id}>
              {w.name} ({w.accountNumber}){w.status === "CLOSED" ? " — closed" : ""}
            </option>
          ))}
        </select>
      </div>

      {loading && <Loading />}
      {!loading && data && data.entries.length === 0 && <Empty text="No transactions yet." />}
      {!loading &&
        data?.entries.map((e) => (
          <div className="history-entry" key={`${e.walletId}-${e.reference}`}>
            <div className="row">
              <strong>{e.type}</strong>
              <StatusPill label={e.entryType} kind={e.entryType} />
            </div>
            <div className="row">
              <span className="muted">
                {e.walletName} · {e.accountNumber}
              </span>
              {e.walletStatus === "CLOSED" && <StatusPill label="Closed" kind="CLOSED" />}
            </div>
            <div className={`history-amount ${e.entryType.toLowerCase()}`}>
              {e.entryType === "CREDIT" ? "+" : "−"}
              {formatMoney(e.amount)}
            </div>
            <div className="row">
              <span className="muted mono" style={{ fontSize: 12 }}>
                {e.reference}
              </span>
              <button
                type="button"
                className="link"
                onClick={() => void navigator.clipboard.writeText(e.reference)}
              >
                Copy
              </button>
            </div>
            <div className="muted" style={{ fontSize: 12 }}>
              {new Date(e.timestamp).toLocaleString()}
            </div>
            {e.type === "PAYMENT" && (
              <div>
                <Link to="/refund" state={{ reference: e.reference }}>
                  Refund this payment
                </Link>
              </div>
            )}
          </div>
        ))}
      {data && <Pagination page={data.page} totalPages={data.totalPages} onPage={changePage} />}
      <BottomNav />
    </div>
  );
}
