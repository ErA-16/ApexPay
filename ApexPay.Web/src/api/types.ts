export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
}

export interface CreateAccountResponse {
  id: string;
  fullName: string;
  email: string;
}

export interface WalletResponse {
  id: string;
  accountNumber: string;
  name: string;
  ownerName?: string | null;
  currency: string;
  balance: number;
  status: string;
  createdAt: string;
}

export interface WalletListResponse {
  userId: string;
  page: number;
  limit: number;
  totalWallets: number;
  totalPages: number;
  wallets: WalletResponse[];
}

export interface WalletBriefResponse {
  id: string;
  accountNumber: string;
  name: string;
  ownerName?: string | null;
  currency: string;
  status: string;
}

export interface WalletHistoryEntry {
  reference: string;
  type: string;
  entryType: string;
  amount: number;
  timestamp: string;
}

export interface WalletHistoryResponse {
  walletId: string;
  page: number;
  limit: number;
  totalEntries: number;
  totalPages: number;
  ledgerCalculatedBalance: number;
  walletStoredBalance: number;
  isConsistent: boolean;
  entries: WalletHistoryEntry[];
}

export interface AccountHistoryWallet {
  id: string;
  name: string;
  accountNumber: string;
  status: string;
}

export interface AccountHistoryEntry {
  walletId: string;
  walletName: string;
  accountNumber: string;
  walletStatus: string;
  reference: string;
  type: string;
  entryType: string;
  amount: number;
  timestamp: string;
}

export interface AccountHistoryResponse {
  walletId: string | null;
  page: number;
  limit: number;
  totalEntries: number;
  totalPages: number;
  wallets: AccountHistoryWallet[];
  entries: AccountHistoryEntry[];
}

export interface FundWalletResponse {
  transactionId: string;
  reference: string;
  walletId: string;
  amount: number;
  accountNumber: string;
  accountName: string;
  narration: string | null;
  state: string;
  newBalance: number;
}

export interface WalletTransferResponse {
  transactionId: string;
  reference: string;
  sourceWalletId: string;
  sourceAccountNumber: string;
  sourceAccountName: string;
  destinationWalletId: string;
  destinationAccountNumber: string;
  destinationAccountName: string;
  amount: number;
  newBalance: number;
  narration: string | null;
  state: string;
}

export interface WalletCheckoutResponse {
  transactionId: string;
  reference: string;
  orderId: string;
  userWalletId: string;
  userAccountNumber: string;
  userAccountName: string;
  merchantWalletId: string;
  merchantAccountNumber: string;
  merchantAccountName: string;
  amount: number;
  newBalance: number;
  state: string;
}

export interface RefundResponse {
  refundTransactionId: string;
  reference: string;
  originalTransactionReference: string;
  merchantWalletId: string;
  creditedWalletId: string;
  creditedAccountNumber: string;
  creditedWalletName: string;
  createdWallet: boolean;
  amount: number;
  reason: string | null;
  state: string;
}

export class ApiError extends Error {
  status: number;

  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}

function extractProblemDetail(body: string): string | null {
  try {
    const parsed = JSON.parse(body) as Record<string, unknown>;
    if (!parsed || typeof parsed !== "object") return null;
    if (typeof parsed["title"] === "string" && parsed["title"].length > 0) return parsed["title"];
    const errors = parsed["errors"] as Record<string, string[]> | undefined;
    if (errors && typeof errors === "object") {
      const first = Object.values(errors)[0];
      if (Array.isArray(first) && first.length > 0) return first[0];
    }
    if (typeof parsed["detail"] === "string" && parsed["detail"].length > 0) return parsed["detail"];
    return null;
  } catch {
    return null;
  }
}

export function friendlyMessage(status: number, body: string): string {
  if (!body) {
    if (status === 401) return "Your session has expired. Please log in again.";
    if (status === 404) return "Not found.";
    if (status === 409) return "This action was already processed. Please check the latest result.";
    if (status === 429) return "Too many attempts. Please wait a minute and try again.";
    if (status >= 500) return "Something went wrong on our side. Please try again.";
    return "Something went wrong. Please try again.";
  }
  if (/Idempotency-Key/i.test(body)) {
    return "This action was already processed. Please check the latest result.";
  }
  const extracted = extractProblemDetail(body);
  if (extracted) {
    return extracted;
  }
  const text = body.length > 300 ? body.slice(0, 300) : body;
  return text;
}
