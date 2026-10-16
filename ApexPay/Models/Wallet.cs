namespace ApexPay.Models
{
    public class Wallet
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string AccountNumber { get; set; } = null!;
        public string Name { get; set; } = null!;
        public Guid AccountId { get; set; }
        public Account UserAccount { get; set; } = null!;
        public string Currency { get; set; } = "NGN";

        public decimal Balance { get; set; } = 0.00m;

        public WalletStatus Status = WalletStatus.Active;
        public WalletRole Role = WalletRole.User;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
