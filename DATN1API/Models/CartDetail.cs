using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace DATN1API.Models;

public partial class CartDetail
{
    public int CartDetailId { get; set; }

    public int? CartId { get; set; }

    public int? ProductVariantId { get; set; }  // Thêm thuộc tính này

    public int? Quantity { get; set; }

    public virtual Cart? Cart { get; set; }

    [ForeignKey(nameof(ProductVariantId))]
    public virtual ProductVariant? ProductVariant { get; set; }  // Liên kết đến ProductVariant
}
