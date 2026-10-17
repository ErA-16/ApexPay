import { useState } from "react";
import { useLocation } from "react-router-dom";
import { api, newIdempotencyKey } from "../api/client.ts";
import type { RefundResponse } from "../api/types.ts";
import { ConfirmDialog, ErrorBanner, OkBanner, TopNav } from "../components/ui.tsx";
import { formatMoney } from "../money.ts";

export function RefundPage() {
  const location = useLocation();
  const presetReference = (location.state as { reference?: string } | null)?.reference ?? "";
  const [reference, setReference] = useState(presetReference);
  const [reason, setReason] = useState("");
  const [error, setError] = useState("");
  const [result, setResult] = useState<RefundResponse | null>(null);
  const [sending, setSending] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [dialogError, setDialogError] = useState("");

  const openConfirm = (event: React.FormEvent) => {
    event.preventDefault();
    if (!reference.trim()) {
      setError("Enter the original transaction reference.");
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
      const data = await api<RefundResponse>(
        "/api/wallet/refund",
        "POST",
        {
          originalTransactionReference: reference.trim(),
          reason: reason.trim() || undefined,
        },
        { idempotencyKey: newIdempotencyKey() },
      );
      setResult(data);
      setConfirmOpen(false);
      setReference("");
      setReason("");
    } catch (err) {
      setDialogError(err instanceof Error ? err.message : "Refund failed.");
    } finally {
      setSending(false);
    }
  };

  return (
    <div className="page">
      <TopNav title="Refund" />
      <h1>Refund a payment</h1>
      {error && <ErrorBanner message={error} />}
      {result && (
        <OkBanner
          message={`Refunded ${formatMoney(result.amount)} for ${result.originalTransactionReference}. Ref: ${result.reference}`}
        />
      )}
      <div className="card">
        <form onSubmit={openConfirm}>
          <label htmlFor="refund-ref">Original transaction reference</label>
          <input id="refund-ref" className="mono" value={reference} onChange={(e) => setReference(e.target.value)} placeholder="TXN-..." />
          <label htmlFor="refund-reason">Reason (optional)</label>
          <input id="refund-reason" value={reason} onChange={(e) => setReason(e.target.value)} maxLength={200} />
          <button type="submit" disabled={sending}>
            {sending ? "Refunding…" : "Review refund"}
          </button>
        </form>
      </div>
      <ConfirmDialog
        open={confirmOpen}
        title="Confirm refund"
        amount="Full remaining amount"
        details={[
          { label: "Original ref", value: reference },
          { label: "Reason", value: reason || "—" },
        ]}
        confirmLabel="Process refund"
        sending={sending}
        error={dialogError}
        onConfirm={() => void submit()}
        onCancel={() => setConfirmOpen(false)}
      />
    </div>
  );
}
