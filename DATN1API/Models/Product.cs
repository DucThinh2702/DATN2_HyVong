using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace DATN1API.Models;

public partial class Product
{
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? Description { get; set; }
    public decimal? SalePrice { get; set; }
    public decimal? OriginalPrice { get; set; }
    public string? Material { get; set; }
    public int? CategoryId { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public string? ThumbnailImage { get; set; }
    public string? Status { get; set; }

    [NotMapped]
    public IFormFile? ImageFile { get; set; }

    [BindNever]
    public virtual Category? Category { get; set; }


    public virtual ICollection<CartDetail> CartDetails { get; set; } = new List<CartDetail>();
    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    public virtual ICollection<ProductVariant> ProductVariants { get; set; } = new List<ProductVariant>();
}
