using System;
using System.Collections.Generic;

namespace DATN1WEB.Models.ViewModels
{
    public class MyOrderListItemVM
    {
        public int OrderId { get; set; }
        public DateTime OrderDate { get; set; }
        public int Quantity { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentStatus { get; set; } = "";
        public string OrderStatus { get; set; } = "";
        public string RecipientName { get; set; } = "";
        public string RecipientPhone { get; set; } = "";
        public string DeliveryAddress { get; set; } = "";
        public string? Note { get; set; }
    }

    public class MyOrderDetailItemVM
    {
        public int ProductVariantId { get; set; }
        public string ProductName { get; set; } = "";
        public string? ColorName { get; set; }
        public string? SizeName { get; set; }
        public string? ThumbnailImage { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class MyOrderDetailVM
    {
        public int OrderId { get; set; }
        public DateTime OrderDate { get; set; }
        public int Quantity { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentStatus { get; set; } = "";
        public string OrderStatus { get; set; } = "";
        public string RecipientName { get; set; } = "";
        public string RecipientPhone { get; set; } = "";
        public string DeliveryAddress { get; set; } = "";
        public string? Note { get; set; }

        public List<MyOrderDetailItemVM> Items { get; set; } = new();
    }
    public class UpdateOrderInfoRequest
    {
        public int OrderId { get; set; }
        public string? RecipientName { get; set; }
        public string? RecipientPhone { get; set; }
        public string? DeliveryAddress { get; set; }
        public string? Note { get; set; }
    }
}
