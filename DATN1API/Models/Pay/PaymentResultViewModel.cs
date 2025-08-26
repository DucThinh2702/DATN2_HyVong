namespace DATN1API.Models.Pay
{
    public class PaymentResultViewModel
    {
        public bool Success { get; set; }
        public long OrderId { get; set; }
        public long TransactionId { get; set; }
        public long Amount { get; set; } // VND (đã chia 100)
        public string? BankCode { get; set; }
        public string? Message { get; set; }
        public DateTime? PaidAt { get; set; }
        public string? ResponseCode { get; set; }
    }
}
