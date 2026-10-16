using System.ComponentModel.DataAnnotations;

namespace ApexPay.Dto
{
    public class WalletTransferRequest
    {
        [Required]
        public Guid? DestinationWalletId { get; set; }

        [Range(typeof(decimal), "100", "10000000", ErrorMessage = "Amount must be between 100 and 10,000,000")]
        public decimal Amount { get; set; }

        [StringLength(200)]
        public string? Narration { get; set; }
    }
}
