using DATN1WEB.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace DATN1API.Models
{
    public partial class Order
    {
        public int OrderId { get; set; }
        public string? UserId { get; set; }
        public DateTime? OrderDate { get; set; }
        public int? Quantity { get; set; }
        public decimal? TotalAmount { get; set; }
        public int? PaymentId { get; set; }
        public string? PaymentStatus { get; set; }
        public string? OrderStatus { get; set; }
        public string? RecipientName { get; set; }
        public string? RecipientPhone { get; set; }
        public string? DeliveryAddress { get; set; }
        public string? Note { get; set; }

        // Mã public dành cho khách (ví dụ "ABCD1234")
        public int? PromoCode { get; set; }   // FK → Promotion.PromoCode

        public decimal? ShippingFee { get; set; }

        // 🔥 Đây mới là FK int -> Promotions.PromoCode
        public int? PromotionPromoCode { get; set; }

        [ForeignKey(nameof(PromotionPromoCode))]
        public virtual Promotion? Promotion { get; set; }

        public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }
    }
}