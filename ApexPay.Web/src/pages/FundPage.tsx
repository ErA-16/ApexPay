import { useState } from "react";
import { useLocation } from "react-router-dom";
import { api, newIdempotencyKey } from "../api/client.ts";
import type { FundWalletResponse } from "../api/types.ts";
import { useWallets } from "../hooks/useWallets.ts";
import { ConfirmDialog, ErrorBanner, OkBanner, TopNav, WalletSelect } from "../components/ui.tsx";
import { amountToNumber, formatMoney, validateAmount } from "../money.ts";

export function FundPage() {
  const location = useLocation();
  const { wallets } = useWallets();
  const presetWalletId = (location.state as { walletId?: string } | null)?.walletId ?? "";

  const [walletId, setWalletId] = useState(presetWalletId);
  const [amount, setAmount] = useState("");
  const [narration, setNarration] = useState("");
  const [error, setError] = useState("");
  const [result, setResult] = useState<FundWalletResponse | null>(null);
  const [sending, setSending] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [dialogError, setDialogError] = useState("");

  const targetWallet = wallets.find((w) => w.id === walletId);
  const liveAmountError = amount.trim() ? validateAmount(amount) : null;

  const openConfirm = (event: React.FormEvent) => {
    event.preventDefault();
    if (!walletId) {
      setError("Select a wallet first.");
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
        { walletId, amount: amountToNumber(amount), narration: narration.trim() || undefined },
        { idempotencyKey: newIdempotencyKey() },
      );
      setResult(data);
      setConfirmOpen(false);
      setAmount("");
      setNarration("");
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
          <button type="submit" disabled={sending || !walletId}>
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
