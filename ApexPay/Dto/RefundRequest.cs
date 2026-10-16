using System.ComponentModel.DataAnnotations;

namespace ApexPay.Dto
{
    public class RefundRequest
    {
        [Required]
        public string OriginalTransactionReference { get; set; } = null!;

        [StringLength(200)]
        public string? Reason { get; set; }
    }
}
