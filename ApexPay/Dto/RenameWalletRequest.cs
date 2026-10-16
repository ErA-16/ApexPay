using System.ComponentModel.DataAnnotations;

namespace ApexPay.Dto
{
    public class RenameWalletRequest
    {
        [Required]
        [StringLength(50)]
        public string Name { get; set; } = null!;
    }
}