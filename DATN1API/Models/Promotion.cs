using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DATN1API.Models;

public partial class Promotion
{
    [Key]
    public int PromoCode { get; set; }

    [Required(ErrorMessage = "Tên khuyến mãi không được bỏ trống")]
    [StringLength(100, ErrorMessage = "Tên khuyến mãi không được dài quá 100 ký tự")]
    public string? PromoName { get; set; }

    [Required(ErrorMessage = "Loại khuyến mãi không được để trống")]
    [StringLength(50, ErrorMessage = "Loại khuyến mãi không được dài quá 50 ký tự")]
    public string? PromoType { get; set; }

    [Required(ErrorMessage = "Giá trị giảm không được bỏ trống")]
    [Range(0, 1000000, ErrorMessage = "Giá trị giảm phải lớn hơn 0")]
    [RegularExpression(@"^\d+$", ErrorMessage = "Giá trị giảm chỉ được phép là số và không có ký tự đặc biệt")]
    [DisplayFormat(DataFormatString = "{0:0}", ApplyFormatInEditMode = true)]
    public decimal? DiscountValue { get; set; }

    [Required(ErrorMessage = "Đơn hàng tối thiểu không được bỏ trống")]
    [Range(0, 1000000, ErrorMessage = "Đơn hàng tối thiểu phải lớn hơn 0")]
    [RegularExpression(@"^\d+$", ErrorMessage = "Giá trị đơn hàng tối thiểu chỉ được phép là số và không có ký tự đặc biệt")]
    [DisplayFormat(DataFormatString = "{0:0}", ApplyFormatInEditMode = true)]
    public decimal? MinOrderAmount { get; set; }

    [DataType(DataType.Date)]
    [Required(ErrorMessage = "Vui lòng chọn ngày bắt đầu")]
    public DateTime? StartDate { get; set; }

    [DataType(DataType.Date)]
    [Required(ErrorMessage = "Vui lòng chọn ngày kết thúc")]
    public DateTime? EndDate { get; set; }

    [Required(ErrorMessage = "Số lượng khuyến mãi là bắt buộc")]
    [Range(1, 100000, ErrorMessage = "Số lượng phải từ 1 đến 100000")]
    [RegularExpression(@"^\d+$", ErrorMessage = "Số lượng phải là số nguyên và không có ký tự đặc biệt")]
    public int? Quantity { get; set; }

    [Range(0, 100000, ErrorMessage = "Số lượng đã sử dụng không được âm")]
    public int? UsedQuantity { get; set; }

    [StringLength(50)]
    public string? Status { get; set; }
    // Thêm Description mới vào đây
    [Required(ErrorMessage = "Mô tả không được bỏ trống")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "Mô tả phải từ 10 đến 500 ký tự")]
    public string? Description { get; set; }
    // Thêm PromoNameCode, nó sẽ được tạo tự động
    public string? PromoNameCode { get; set; } = GeneratePromoNameCode();
    [StringLength(10000)]
    // Mối quan hệ một Promotion có thể có nhiều ShippingProviders
    public string? ShippingProviderName { get; set; }  // Lưu tên đơn vị vận chuyển hoặc mã của đơn vị vận chuyển (nếu cần)
    //[MinCount(1)] // Custom validate yêu cầu ít nhất 1 item
    public ICollection<ShippingProvider>? ShippingProviders { get; set; }


    // Phương thức tạo mã khuyến mãi ngẫu nhiên
    public static string GeneratePromoNameCode()
    {
        var random = new Random();
        var letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var digits = "0123456789";

        // Sinh phần chữ
        var letterPart = new string(Enumerable.Range(0, 5)
            .Select(_ => letters[random.Next(letters.Length)])
            .ToArray());

        // Sinh phần số
        var numberPart = new string(Enumerable.Range(0, 4)
            .Select(_ => digits[random.Next(digits.Length)])
            .ToArray());

        return $"{letterPart}{numberPart}";
    }
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
