namespace DATN1API.Models.Pay
{
    public class PayOSOptions
    {
        public string BaseUrl { get; set; } = "";
        public string ClientId { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public string ChecksumKey { get; set; } = "";
        public string WebhookSecret { get; set; } = "";
        public string ReturnUrl { get; set; } = "";
        public string CancelUrl { get; set; } = "";
    }


}
