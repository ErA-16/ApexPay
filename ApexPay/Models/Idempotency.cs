namespace ApexPay.Models
{
    public class Idempotency
    {
        public string Key { get; set; } = null!;
        public string RequestPath { get; set; } = null!;
        public int? ResponseCode { get; set; }
        public string? ResponseBody { get; set; }
        public IdempotencyStatus Status { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
