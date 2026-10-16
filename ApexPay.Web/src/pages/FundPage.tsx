import { useState } from "react";
import { useLocation } from "react-router-dom";
import { api, newIdempotencyKey } from "../api/client.ts";
import type { FundWalletResponse, WalletBriefResponse } from "../api/types.ts";
import { useWallets } from "../hooks/useWallets.ts";
import { ConfirmDialog, ErrorBanner, OkBanner, TopNav, WalletSelect } from "../components/ui.tsx";
import { amountToNumber, formatMoney, validateAmount } from "../money.ts";

export function FundPage() {
  const location = useLocation();
  const { wallets } = useWallets();
  const presetWalletId = (location.state as { walletId?: string } | null)?.walletId ?? "";

  const [walletId, setWalletId] = useState(presetWalletId);
  const [accountNumber, setAccountNumber] = useState("");
  const [resolved, setResolved] = useState<WalletBriefResponse | null>(null);
  const [resolving, setResolving] = useState(false);
  const [amount, setAmount] = useState("");
  const [narration, setNarration] = useState("");
  const [error, setError] = useState("");
  const [result, setResult] = useState<FundWalletResponse | null>(null);
  const [sending, setSending] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [dialogError, setDialogError] = useState("");

  const targetWalletId = walletId || resolved?.id || "";
  const targetWallet = walletId ? wallets.find((w) => w.id === walletId) : resolved;
  const liveAmountError = amount.trim() ? validateAmount(amount) : null;

  const resolve = async () => {
    const num = accountNumber.trim();
    if (!num) return;
    setResolving(true);
    setError("");
    setResolved(null);
    try {
      const found = await api<WalletBriefResponse>(`/api/wallet/by-account-number/${num}`, "GET");
      setResolved(found);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Wallet not found.");
    } finally {
      setResolving(false);
    }
  };

  const openConfirm = (event: React.FormEvent) => {
    event.preventDefault();
    if (!targetWalletId) {
      setError("Select or look up a wallet first.");
      return;
    }
    const problem = validateAmount(amount);
    if (problem) {
      setError(problem);
      return;
    }
    setError("");
    setDialogError("");
    setConfirmOpen(true);
  };

  const submit = async () => {
    setSending(true);
    setDialogError("");
    try {
      const data = await api<FundWalletResponse>(
        "/api/wallet/fund",
        "POST",
        { walletId: targetWalletId, amount: amountToNumber(amount), narration: narration.trim() || undefined },
        { idempotencyKey: newIdempotencyKey() },
      );
      setResult(data);
      setConfirmOpen(false);
      setAmount("");
      setNarration("");
      setAccountNumber("");
      setResolved(null);
    } catch (err) {
      setDialogError(err instanceof Error ? err.message : "Funding failed.");
    } finally {
      setSending(false);
    }
  };

  return (
    <div className="page">
      <TopNav title="Fund wallet" />
      <h1>Fund a wallet</h1>
      <div className="banner warn" role="status">
        Test mode — funds added here are not real money.
      </div>
      {error && <ErrorBanner message={error} />}
      {result && (
        <OkBanner message={`Funded ${formatMoney(result.amount)}. New balance: ${formatMoney(result.newBalance)}. Ref: ${result.reference}`} />
      )}
      <div className="card">
        <h2>Choose wallet</h2>
        <WalletSelect wallets={wallets} value={walletId} onChange={setWalletId} label="Your wallet" id="fund-wallet-select" />
        <p className="muted" style={{ margin: "12px 0 0" }}>
          or
        </p>
        <label htmlFor="fund-account-number">Enter account number</label>
        <input
          id="fund-account-number"
          className="mono"
          value={accountNumber}
          onChange={(e) => setAccountNumber(e.target.value)}
          maxLength={10}
          placeholder="10-digit account number"
          inputMode="numeric"
        />
        <button type="button" className="secondary" onClick={() => void resolve()} disabled={resolving || !accountNumber.trim()}>
          {resolving ? "Looking up…" : "Look up"}
        </button>
        {resolved && (
          <div className="banner ok" role="status">
            Found: <strong>{resolved.ownerName || resolved.name}</strong> <span className="mono">{resolved.accountNumber}</span>
          </div>
        )}
      </div>
      <div className="card">
        <h2>Amount</h2>
        <form onSubmit={openConfirm}>
          <label htmlFor="fund-amount">Amount (₦100 – ₦10,000,000)</label>
          <input
            id="fund-amount"
            className="tabular"
            inputMode="decimal"
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
            placeholder="0.00"
          />
          {liveAmountError && <p className="field-error">{liveAmountError}</p>}
          <label htmlFor="fund-narration">Narration (optional)</label>
          <input id="fund-narration" value={narration} onChange={(e) => setNarration(e.target.value)} maxLength={200} />
          <button type="submit" disabled={sending || !targetWalletId}>
            {sending ? "Funding…" : "Review & fund"}
          </button>
        </form>
      </div>
      <ConfirmDialog
        open={confirmOpen}
        title="Confirm funding"
        amount={formatMoney(amountToNumber(amount) || 0)}
        details={[
          { label: "To", value: targetWallet ? `${targetWallet.ownerName || targetWallet.name} — ${targetWallet.name}` : "—" },
          { label: "Account", value: targetWallet?.accountNumber ?? "—" },
        ]}
        confirmLabel="Fund wallet"
        sending={sending}
        error={dialogError}
        onConfirm={() => void submit()}
        onCancel={() => setConfirmOpen(false)}
      />
    </div>
  );
}
