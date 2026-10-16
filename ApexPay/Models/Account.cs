namespace ApexPay.Models
{
    public class Account
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? PasswordHash { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<Wallet> Wallets { get; set; } = new();
    }
}
