export function isValidAmountText(value: string): boolean {
  return /^\d+(\.\d{1,2})?$/.test(value.trim());
}

export function amountToNumber(value: string): number {
  return Number(value.trim());
}

export function isAmountInRange(value: string): boolean {
  const n = amountToNumber(value);
  return Number.isFinite(n) && n >= 100 && n <= 10000000;
}

export function validateAmount(value: string): string | null {
  if (!value.trim()) return "Enter an amount.";
  if (!isValidAmountText(value)) return "Amount can have at most 2 decimal places.";
  if (!isAmountInRange(value)) return "Amount must be between 100 and 10,000,000.";
  return null;
}

export function formatMoney(amount: number, currency = "NGN"): string {
  return `${currency} ${amount.toLocaleString("en-NG", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

export function generateOrderId(): string {
  const ts = Date.now().toString(36).toUpperCase();
  const rand = crypto.randomUUID().slice(0, 6).toUpperCase();
  return `ORD-${ts}-${rand}`;
}
