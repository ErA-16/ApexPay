namespace ApexPay.Models
{
    public class Transaction
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Reference { get; set; } = null!;
        public TransactionType Type { get; set; }
        public TransactionStatus Status { get; set; }
        public decimal Amount { get; set; }

        public Guid? SourceWalletId { get; set; }
        public Wallet? SourceWallet { get; set; }

        public Guid DestinationWalletId { get; set; }
        public Wallet DestinationWallet { get; set; } = null!;

        public Guid? ParentTransactionId { get; set; }

        public string? FailureReason { get; set; }

        public string? OrderId { get; set; }

        public string? Narration { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
