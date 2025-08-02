using DATN1API.Models;

namespace DATN1WEB.Models
{
    public class Size
    {
        public int SizeID { get; set; }  // Mã kích thước
        public string? SizeName { get; set; }  // Tên kích thước
        public virtual ICollection<ProductVariant> ProductVariants { get; set; } = new List<ProductVariant>();

    }

}
