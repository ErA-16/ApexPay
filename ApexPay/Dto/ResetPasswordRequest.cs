using System.ComponentModel.DataAnnotations;

namespace ApexPay.Dto
{
    public class ResetPasswordRequest
    {
        [Required]
        [StringLength(128)]
        public string Token { get; set; } = null!;
        [Required]
        [StringLength(100, MinimumLength = 8)]
        public string NewPassword { get; set; } = null!;
    }
}
