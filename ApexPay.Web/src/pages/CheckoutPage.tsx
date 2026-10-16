import { useEffect, useState } from "react";
import { api, newIdempotencyKey } from "../api/client.ts";
import type { WalletBriefResponse, WalletCheckoutResponse } from "../api/types.ts";
import { useWallets } from "../hooks/useWallets.ts";
import { ConfirmDialog, BottomNav, ErrorBanner, Loading, OkBanner, ScreenHeader, WalletSelect } from "../components/ui.tsx";
import { amountToNumber, formatMoney, generateOrderId, validateAmount } from "../money.ts";

export function CheckoutPage() {
  const { wallets } = useWallets();
  const [merchant, setMerchant] = useState<WalletBriefResponse | null>(null);
  const [merchantLoading, setMerchantLoading] = useState(true);
  const [merchantError, setMerchantError] = useState("");

  const [userWalletId, setUserWalletId] = useState("");
  const [amount, setAmount] = useState("");
  const [orderId, setOrderId] = useState(generateOrderId());
  const [error, setError] = useState("");
  const [result, setResult] = useState<WalletCheckoutResponse | null>(null);
  const [sending, setSending] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [dialogError, setDialogError] = useState("");

  const userWallet = wallets.find((w) => w.id === userWalletId);
  const liveAmountError = amount.trim() ? validateAmount(amount) : null;

  useEffect(() => {
    void (async () => {
      setMerchantLoading(true);
      setMerchantError("");
      try {
        const m = await api<WalletBriefResponse>("/api/wallet/merchant", "GET");
        setMerchant(m);
      } catch (err) {
        setMerchantError(err instanceof Error ? err.message : "Could not load merchant.");
      } finally {
        setMerchantLoading(false);
      }
    })();
  }, []);

  const openConfirm = (event: React.FormEvent) => {
    event.preventDefault();
    if (!userWalletId) {
      setError("Select your wallet.");
      return;
    }
    if (!orderId.trim()) {
      setError("Enter an order ID.");
      return;
    }
    if (userWalletId === merchant?.id) {
      setError("Your wallet and the merchant wallet cannot be the same.");
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
      const data = await api<WalletCheckoutResponse>(
        "/api/wallet/checkout",
        "POST",
        {
          userWalletId,
          merchantWalletId: merchant!.id,
          amount: amountToNumber(amount),
          orderId: orderId.trim(),
        },
        { idempotencyKey: newIdempotencyKey() },
      );
      setResult(data);
      setConfirmOpen(false);
      setAmount("");
      setOrderId(generateOrderId());
    } catch (err) {
      setDialogError(err instanceof Error ? err.message : "Checkout failed.");
    } finally {
      setSending(false);
    }
  };

  return (
    <div className="page with-bottom-nav">
      <ScreenHeader title="Pay merchant" />
      {error && <ErrorBanner message={error} />}
      {result && (
        <OkBanner
          message={`Paid ${formatMoney(result.amount)} for order ${result.orderId}. New balance: ${formatMoney(result.newBalance)}. Ref: ${result.reference}`}
        />
      )}
      <div className="card">
        <h2>Merchant</h2>
        {merchantLoading && <Loading text="Loading merchant…" />}
        {merchantError && <ErrorBanner message={merchantError} />}
        {merchant && (
          <div className="balance-card" style={{ marginBottom: 0 }}>
            <div className="balance-name">{merchant.ownerName || merchant.name}</div>
            <div className="balance-meta mono">{merchant.accountNumber}</div>
          </div>
        )}
      </div>
      <div className="card">
        <h2>Payment details</h2>
        <form onSubmit={openConfirm}>
          <WalletSelect wallets={wallets} value={userWalletId} onChange={setUserWalletId} label="Pay from" id="checkout-wallet" />
          <label htmlFor="checkout-amount">Amount (₦100 – ₦10,000,000)</label>
          <input
            id="checkout-amount"
            className="tabular"
            inputMode="decimal"
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
            placeholder="0.00"
          />
          {liveAmountError && <p className="field-error">{liveAmountError}</p>}
          <label htmlFor="checkout-order">Order ID</label>
          <input id="checkout-order" className="mono" value={orderId} onChange={(e) => setOrderId(e.target.value)} maxLength={64} />
          <button type="submit" disabled={sending || !userWalletId || !merchant}>
            {sending ? "Paying…" : "Review & pay"}
          </button>
        </form>
      </div>
      <ConfirmDialog
        open={confirmOpen}
        title="Confirm payment"
        amount={formatMoney(amountToNumber(amount) || 0)}
        details={[
          { label: "From", value: userWallet ? `${userWallet.name} (${userWallet.accountNumber})` : "—" },
          {
            label: "To",
            value: merchant ? `${merchant.ownerName || merchant.name} (${merchant.accountNumber})` : "—",
          },
          { label: "Order", value: orderId },
        ]}
        confirmLabel="Pay merchant"
        sending={sending}
        error={dialogError}
        onConfirm={() => void submit()}
        onCancel={() => setConfirmOpen(false)}
      />
      <BottomNav />
    </div>
  );
}
