namespace ApexPay.Dto
{
    public class WalletTransferResponse
    {
        public Guid TransactionId { get; set; }
        public string Reference { get; set; } = null!;
        public Guid SourceWalletId { get; set; }
        public string SourceAccountNumber { get; set; } = null!;
        public string SourceAccountName { get; set; } = null!;
        public Guid DestinationWalletId { get; set; }
        public string DestinationAccountNumber { get; set; } = null!;
        public string DestinationAccountName { get; set; } = null!;
        public decimal Amount { get; set; }
        public decimal NewBalance { get; set; }
        public string? Narration { get; set; }
        public string State { get; set; } = null!;
    }
}
