using System.ComponentModel.DataAnnotations;

namespace ApexPay.Dto
{
    public class LoginRequest
    {
        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string Email { get; set; } = null!;
        [Required]
        [StringLength(100)]
        public string Password { get; set; } = null!;
    }
}
