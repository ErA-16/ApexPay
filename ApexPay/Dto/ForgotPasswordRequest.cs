using System.ComponentModel.DataAnnotations;

namespace ApexPay.Dto
{
    public class ForgotPasswordRequest
    {
        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string Email { get; set; } = null!;
    }
}
