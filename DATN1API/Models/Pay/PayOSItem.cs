namespace DATN1API.Models.Pay
{
    public class PayOSItem
    {
        public string name { get; set; } = "";
        public int quantity { get; set; }
        public int price { get; set; } // VND
    }

    public class PayOSCreatePaymentRequest
    {
        public long orderCode { get; set; }
        public long amount { get; set; }        // VND
        public string description { get; set; } = "";
        public string returnUrl { get; set; } = "";
        public string cancelUrl { get; set; } = "";
        public List<PayOSItem> items { get; set; } = new();
        public string signature { get; set; } = ""; // HMAC nếu cần
    }

    public class PayOSCreatePaymentResponse
    {
        public string? checkoutUrl { get; set; }
        public string? qrCodeUrl { get; set; }
        public string? code { get; set; }
        public string? message { get; set; }
        public object? data { get; set; }
    }

}
