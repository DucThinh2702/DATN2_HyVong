namespace DATN1API.Helpers
{
    public class VNPAYSettings
    {
        public string TmnCode { get; set; }
        public string HashSecret { get; set; }
        public string PaymentUrl { get; set; }
        public string ReturnUrl { get; set; }
        public string CancelUrl { get; set; }
        public string IpnUrl { get; set; }
    }

}
