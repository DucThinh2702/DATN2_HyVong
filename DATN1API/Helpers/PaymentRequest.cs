using DATN1API.Models.Pay;

namespace DATN1API.Pay
{

    public class PaymentRequest
    {
        public long orderCode { get; set; }
        public int amount { get; set; }
        public string description { get; set; } = "";
        public string returnUrl { get; set; } = "";
        public string cancelUrl { get; set; } = "";
        public List<PayOSItem> items { get; set; } = new();
    }


}
