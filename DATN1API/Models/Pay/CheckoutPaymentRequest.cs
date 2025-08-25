using System.Collections.Generic;

namespace DATN1API.Models.Pay
{
    public class CheckoutPaymentRequest
    {
        // Thông tin giao hàng
        public string CustomerName { get; set; } = "";
        public string CustomerPhone { get; set; } = "";
        public string FullAddress { get; set; } = "";
        public string? OrderNote { get; set; }

        // Sản phẩm
        public List<CheckoutItem> Items { get; set; } = new();

        // Khuyến mại (phải có public set; để model binder bind được)
        public int? PromoCode { get; set; }            // ví dụ "FREESHIP", "SALE10"
        public string? PromotionType { get; set; }        // "percentage" | "amount" | "free_shipping"
        public decimal? DiscountAmount { get; set; }      // giảm vào hàng
        public decimal? ShippingFee { get; set; }         // phí ship sau khi áp dụng freeship

        // Thông tin tính toán tổng tiền cho VNPay
        public decimal SubTotal { get; set; }             // Tạm tính (chưa bao gồm ship và giảm giá)
        public decimal FinalTotal { get; set; }           // Tổng cuối cùng cần thanh toán
        public decimal OriginalShippingFee { get; set; }  // Phí ship gốc trước khi áp dụng khuyến mại

        // Optional: nếu muốn validate theo danh mục ở server
        public List<CartLineMeta>? CartItemsMeta { get; set; }
    }

    public class CheckoutItem
    {
        public int VariantId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    public class CartLineMeta
    {
        public int? CategoryId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}
