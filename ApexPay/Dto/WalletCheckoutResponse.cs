namespace ApexPay.Dto
{
    public class WalletCheckoutResponse
    {
        public Guid TransactionId { get; set; }
        public string Reference { get; set; } = null!;
        public string OrderId { get; set; } = null!;
        public Guid UserWalletId { get; set; }
        public string UserAccountNumber { get; set; } = null!;
        public string UserAccountName { get; set; } = null!;
        public Guid MerchantWalletId { get; set; }
        public string MerchantAccountNumber { get; set; } = null!;
        public string MerchantAccountName { get; set; } = null!;
        public decimal Amount { get; set; }
        public decimal NewBalance { get; set; }
        public string State { get; set; } = null!;
    }
}
