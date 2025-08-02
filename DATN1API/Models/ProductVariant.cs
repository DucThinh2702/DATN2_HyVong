using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DATN1WEB.Models;
using Microsoft.EntityFrameworkCore;

namespace DATN1API.Models;

[Index(nameof(ProductId), nameof(ColorId), nameof(SizeId), IsUnique = true, Name = "UQ_Product_Color_Size")]
[Index(nameof(Sku), IsUnique = true, Name = "UQ_ProductVariant_Sku")]
public partial class ProductVariant
{
    [Key]
    [Column("VariantID")]
    public int VariantId { get; set; }

    [Required]
    [Column("ProductID")]
    public int ProductId { get; set; }

    [Required]
    [Column("ColorID")]
    public int ColorId { get; set; }

    [Required]
    [Column("SizeID")]
    public int SizeId { get; set; }

    [Required]
    [StringLength(100)]
    public string Sku { get; set; } = null!;

    [Display(Name = "Tồn kho")]
    public int? Stock { get; set; }

    [Display(Name = "Giá bán")]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? SalePrice { get; set; }

    [Display(Name = "Giá gốc")]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? OriginalPrice { get; set; }

    [StringLength(225)]
    public string? ThumbnailImage { get; set; }

    [StringLength(50)]
    public string? Status { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? UpdatedDate { get; set; }

    [ForeignKey(nameof(ProductId))]
    [InverseProperty("ProductVariants")]
    public virtual Product Product { get; set; } = null!;

    [ForeignKey(nameof(ColorId))]
    [InverseProperty("ProductVariants")]
    public virtual Color Color { get; set; } = null!;

    [ForeignKey(nameof(SizeId))]
    [InverseProperty("ProductVariants")]
    public virtual Size Size { get; set; } = null!;

    [NotMapped]   // Không map vào database
    public IFormFile? ImageFile { get; set; }

    [InverseProperty("ProductVariant")]
    public virtual ICollection<CartDetail> CartDetails { get; set; } = new List<CartDetail>();

    [InverseProperty("ProductVariant")]
    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}
