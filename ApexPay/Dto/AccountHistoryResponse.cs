namespace ApexPay.Dto
{
    public class AccountHistoryResponse
    {
        public Guid? WalletId { get; set; }
        public int Page { get; set; }
        public int Limit { get; set; }
        public int TotalEntries { get; set; }
        public int TotalPages { get; set; }
        public List<AccountHistoryWallet> Wallets { get; set; } = new List<AccountHistoryWallet>();
        public List<AccountHistoryEntry> Entries { get; set; } = new List<AccountHistoryEntry>();
    }

    public class AccountHistoryWallet
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string AccountNumber { get; set; } = null!;
        public string Status { get; set; } = null!;
    }

    public class AccountHistoryEntry
    {
        public Guid WalletId { get; set; }
        public string WalletName { get; set; } = null!;
        public string AccountNumber { get; set; } = null!;
        public string WalletStatus { get; set; } = null!;
        public string Reference { get; set; } = null!;
        public string Type { get; set; } = null!;
        public string EntryType { get; set; } = null!;
        public decimal Amount { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
