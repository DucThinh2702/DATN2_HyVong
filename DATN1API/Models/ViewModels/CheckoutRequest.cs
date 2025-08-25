using System.Collections.Generic;

namespace DATN1API.Models.ViewModels
{
    public class CheckoutRequest
{
    public string? UserId { get; set; }
    public string? RecipientName { get; set; }
    public string? RecipientPhone { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? Note { get; set; }
    public int? PromoCode { get; set; }
    public decimal? ShippingFee { get; set; }

    public List<CheckoutItem> Items { get; set; } = new();
}

public class CheckoutItem
{
    public int ProductVariantId { get; set; }   // <- CHỈNH CHỖ NÀY nếu đang là string
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}



}
