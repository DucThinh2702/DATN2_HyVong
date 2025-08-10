using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using DATN1WEB.Models;
using Microsoft.EntityFrameworkCore;

namespace DATN1API.Models
{
    [Index(nameof(ProductId), nameof(ColorId), nameof(SizeId), IsUnique = true, Name = "UQ_Product_Color_Size")]
    [Index(nameof(Sku), IsUnique = true, Name = "UQ_ProductVariant_Sku")]
    public partial class ProductVariant
    {
        [Key]
        [Column("VariantID")]
        public int VariantId { get; set; }

        [Column("ProductID")]
        public int ProductId { get; set; }

        [Column("ColorID")]
        public int ColorId { get; set; }

        [Column("SizeID")]
        public int SizeId { get; set; }

        // Cho phép null để API tự sinh SKU
        [StringLength(100)]
        public string? Sku { get; set; }

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

        // Navigation không bắt buộc khi create/update
        [JsonIgnore]
        [ForeignKey(nameof(ProductId))]
        [InverseProperty("ProductVariants")]
        public virtual Product? Product { get; set; }

        [ForeignKey(nameof(ColorId))]
        [InverseProperty("ProductVariants")]
        public virtual Color? Color { get; set; }

        [ForeignKey(nameof(SizeId))]
        [InverseProperty("ProductVariants")]
        public virtual Size? Size { get; set; }

        [NotMapped]
        public IFormFile? ImageFile { get; set; }

        //[JsonIgnore]
        //[InverseProperty("ProductVariant")]
        //public virtual ICollection<CartDetail> CartDetails { get; set; } = new List<CartDetail>();

        [JsonIgnore]
        [InverseProperty("ProductVariant")]
        public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    }
}
