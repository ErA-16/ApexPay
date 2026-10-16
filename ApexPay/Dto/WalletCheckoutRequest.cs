using System.ComponentModel.DataAnnotations;

namespace ApexPay.Dto
{
    public class WalletCheckoutRequest
    {
        [Required]
        public Guid? UserWalletId { get; set; }

        [Required]
        public Guid? MerchantWalletId { get; set; }

        [Range(typeof(decimal), "100", "10000000", ErrorMessage = "Amount must be between 100 and 10,000,000")]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(64)]
        public string OrderId { get; set; } = null!;
    }
}
