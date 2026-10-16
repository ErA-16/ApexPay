using ApexPay.Models;

namespace ApexPay.Dto
{
    public class FundWalletResponse
    {
        public Guid TransactionId { get; set; }
        public string Reference { get; set; } = null!;
        public Guid WalletId { get; set; }
        public decimal Amount { get; set; }
        public string AccountNumber { get; set; } = null!;
        public string AccountName { get; set; } = null!;
        public string? Narration { get; set; }
        public string State { get; set; } = null!;
        public decimal NewBalance { get; set; }
    }
}
