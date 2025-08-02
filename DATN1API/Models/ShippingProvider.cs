using DATN1API.Models;
using System;

public class ShippingProvider
    {
        public int ShippingProviderId { get; set; }
        public string? ShippingProviderName { get; set; }
        public string? ApiCode { get; set; }  // Mã API của đơn vị vận chuyển

        // Khóa ngoại tham chiếu đến Promotion
        public int? PromoCode { get; set; }
        public Promotion? Promotion { get; set; }  // Mối quan hệ với Promotion
    }



