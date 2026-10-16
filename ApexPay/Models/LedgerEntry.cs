namespace ApexPay.Models
{
    public class LedgerEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TransactionId { get; set; }
        public Transaction Transaction { get; set; } = null!;

        public Guid WalletId { get; set; }
        public Wallet Wallet { get; set; } = null!;

        public LedgerEntryType EntryType { get; set; }

        public decimal Amount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
