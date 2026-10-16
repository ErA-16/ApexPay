using System.ComponentModel.DataAnnotations;

namespace ApexPay.Dto
{
    public class FundWalletRequest
    {
        public Guid WalletId { get; set; }
        [Required]
        [Range(typeof(decimal), "100", "10000000", ErrorMessage = "Amount must be between 100 and 10,000,000")]
        public decimal Amount { get; set; }
        [StringLength(200)]
        public string? Narration { get; set; }
    }
}
