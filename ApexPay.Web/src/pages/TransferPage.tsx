import { useState } from "react";
import { useLocation } from "react-router-dom";
import { api, newIdempotencyKey } from "../api/client.ts";
import type { WalletBriefResponse, WalletTransferResponse } from "../api/types.ts";
import { useWallets } from "../hooks/useWallets.ts";
import { ConfirmDialog, BottomNav, ErrorBanner, OkBanner, ScreenHeader, WalletSelect } from "../components/ui.tsx";
import { amountToNumber, formatMoney, validateAmount } from "../money.ts";

export function TransferPage() {
  const location = useLocation();
  const { wallets } = useWallets();
  const presetFrom = (location.state as { fromWalletId?: string } | null)?.fromWalletId ?? "";

  const [fromWalletId, setFromWalletId] = useState(presetFrom);
  const [destinationNumber, setDestinationNumber] = useState("");
  const [destination, setDestination] = useState<WalletBriefResponse | null>(null);
  const [resolving, setResolving] = useState(false);
  const [amount, setAmount] = useState("");
  const [narration, setNarration] = useState("");
  const [error, setError] = useState("");
  const [result, setResult] = useState<WalletTransferResponse | null>(null);
  const [sending, setSending] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [dialogError, setDialogError] = useState("");

  const sourceWallet = wallets.find((w) => w.id === fromWalletId);
  const liveAmountError = amount.trim() ? validateAmount(amount) : null;

  const resolve = async () => {
    const num = destinationNumber.trim();
    if (!num) return;
    setResolving(true);
    setError("");
    setDestination(null);
    try {
      const found = await api<WalletBriefResponse>(`/api/wallet/by-account-number/${num}`, "GET");
      setDestination(found);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Wallet not found.");
    } finally {
      setResolving(false);
    }
  };

  const openConfirm = (event: React.FormEvent) => {
    event.preventDefault();
    if (!fromWalletId) {
      setError("Select a wallet to send from.");
      return;
    }
    if (!destination) {
      setError("Look up the destination account number first.");
      return;
    }
    if (destination.id === fromWalletId) {
      setError("Source and destination wallet cannot be the same.");
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
      const data = await api<WalletTransferResponse>(
        `/api/wallet/${fromWalletId}/transfer`,
        "POST",
        {
          destinationWalletId: destination!.id,
          amount: amountToNumber(amount),
          narration: narration.trim() || undefined,
        },
        { idempotencyKey: newIdempotencyKey() },
      );
      setResult(data);
      setConfirmOpen(false);
      setAmount("");
      setNarration("");
      setDestinationNumber("");
      setDestination(null);
    } catch (err) {
      setDialogError(err instanceof Error ? err.message : "Transfer failed.");
    } finally {
      setSending(false);
    }
  };

  return (
    <div className="page with-bottom-nav">
      <ScreenHeader title="Send money" />
      {error && <ErrorBanner message={error} />}
      {result && (
        <OkBanner
          message={`Sent ${formatMoney(result.amount)} to ${result.destinationAccountName}. New balance: ${formatMoney(result.newBalance)}. Ref: ${result.reference}`}
        />
      )}
      <div className="card">
        <h2>From</h2>
        <WalletSelect wallets={wallets} value={fromWalletId} onChange={setFromWalletId} label="Send from" id="transfer-from" />
      </div>
      <div className="card">
        <h2>To</h2>
        <label htmlFor="dest-number">Destination account number</label>
        <input
          id="dest-number"
          className="mono"
          value={destinationNumber}
          onChange={(e) => setDestinationNumber(e.target.value)}
          maxLength={10}
          placeholder="10-digit account number"
          inputMode="numeric"
        />
        <button type="button" className="secondary" onClick={() => void resolve()} disabled={resolving || !destinationNumber.trim()}>
          {resolving ? "Looking up…" : "Look up"}
        </button>
        {destination && (
          <div className="banner ok" role="status">
            Sending to: <strong>{destination.ownerName || destination.name}</strong>{" "}
            <span className="muted">({destination.name})</span> <span className="mono">{destination.accountNumber}</span>
          </div>
        )}
      </div>
      <div className="card">
        <h2>Amount</h2>
        <form onSubmit={openConfirm}>
          <label htmlFor="transfer-amount">Amount (₦100 – ₦10,000,000)</label>
          <input
            id="transfer-amount"
            className="tabular"
            inputMode="decimal"
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
            placeholder="0.00"
          />
          {liveAmountError && <p className="field-error">{liveAmountError}</p>}
          <label htmlFor="transfer-narration">Narration (optional)</label>
          <input id="transfer-narration" value={narration} onChange={(e) => setNarration(e.target.value)} maxLength={200} />
          <button type="submit" disabled={sending || !fromWalletId || !destination}>
            {sending ? "Sending…" : "Review & send"}
          </button>
        </form>
      </div>
      <ConfirmDialog
        open={confirmOpen}
        title="Confirm transfer"
        amount={formatMoney(amountToNumber(amount) || 0)}
        details={[
          { label: "From", value: sourceWallet ? `${sourceWallet.name} (${sourceWallet.accountNumber})` : "—" },
          {
            label: "To",
            value: destination
              ? `${destination.ownerName || destination.name} — ${destination.name} (${destination.accountNumber})`
              : "—",
          },
        ]}
        confirmLabel="Send money"
        sending={sending}
        error={dialogError}
        onConfirm={() => void submit()}
        onCancel={() => setConfirmOpen(false)}
      />
      <BottomNav />
    </div>
  );
}
