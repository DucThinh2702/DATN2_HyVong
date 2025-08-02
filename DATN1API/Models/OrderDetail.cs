using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace DATN1API.Models;
public partial class OrderDetail
{
    public int OrderDetailId { get; set; }

    public int? OrderId { get; set; }

    public int? ProductVariantId { get; set; }  // Thêm thuộc tính này

    public int? Quantity { get; set; }

    public decimal? UnitPrice { get; set; }

    public decimal? TotalPrice { get; set; }

    [ForeignKey(nameof(OrderId))]
    public virtual Order? Order { get; set; }

    [ForeignKey(nameof(ProductVariantId))]
    public virtual ProductVariant? ProductVariant { get; set; }  // Liên kết đến ProductVariant
}
