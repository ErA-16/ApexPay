namespace ApexPay.Dto
{
    public class WalletListResponse
    {
        public Guid UserId { get; set; }
        public int Page { get; set; }
        public int Limit { get; set; }
        public int TotalWallets { get; set; }
        public int TotalPages { get; set; }
        public List<WalletResponse> Wallets { get; set; } = new List<WalletResponse>();
    }
}
