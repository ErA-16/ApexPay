namespace ApexPay.Dto
{
    public class RefundResponse
    {
        public Guid RefundTransactionId { get; set; }
        public string Reference { get; set; } = null!;
        public string OriginalTransactionReference { get; set; } = null!;
        public Guid MerchantWalletId { get; set; }
        public Guid CreditedWalletId { get; set; }
        public string CreditedAccountNumber { get; set; } = null!;
        public string CreditedWalletName { get; set; } = null!;
        public bool CreatedWallet { get; set; }
        public decimal Amount { get; set; }
        public string? Reason { get; set; }
        public string State { get; set; } = null!;
    }
}
