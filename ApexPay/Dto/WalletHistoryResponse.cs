namespace ApexPay.Dto
{
    public class WalletHistoryResponse
    {
        public Guid WalletId { get; set; }
        public int Page { get; set; }
        public int Limit { get; set; }
        public int TotalEntries { get; set; }
        public int TotalPages { get; set; }
        public decimal LedgerCalculatedBalance { get; set; }
        public decimal WalletStoredBalance { get; set; }
        public bool IsConsistent { get; set; }
        public List<WalletHistoryEntry> Entries { get; set; } = new List<WalletHistoryEntry>();
    }

    public class WalletHistoryEntry
    {
        public string Reference { get; set; } = null!;
        public string Type { get; set; } = null!;
        public string EntryType { get; set; } = null!;
        public decimal Amount { get; set; }
        public DateTime Timestamp { get; set; }
    }
}