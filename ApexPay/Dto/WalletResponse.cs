namespace ApexPay.Dto
{
    public class WalletResponse
    {
        public Guid Id { get; set; }
        public string AccountNumber { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? OwnerName { get; set; }
        public string Currency { get; set; } = null!;
        public decimal Balance { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
