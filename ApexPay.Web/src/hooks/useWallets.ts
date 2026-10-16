import { useCallback, useEffect, useState } from "react";
import { api } from "../api/client.ts";
import type { WalletListResponse, WalletResponse } from "../api/types.ts";

interface UseWalletsResult {
  wallets: WalletResponse[];
  loading: boolean;
  error: string;
  reload: () => Promise<void>;
}

export function useWallets(): UseWalletsResult {
  const [wallets, setWallets] = useState<WalletResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const reload = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const data = await api<WalletListResponse>("/api/wallet/wallets?page=1&limit=100", "GET");
      setWallets(data.wallets);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not load wallets.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void reload();
  }, [reload]);

  return { wallets, loading, error, reload };
}
