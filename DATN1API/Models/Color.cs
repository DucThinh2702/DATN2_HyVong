
using DATN1API.Models;

namespace DATN1WEB.Models
{
    public class Color
    {
        public int ColorID { get; set; }  // Mã màu
        public string? ColorName { get; set; }  // Tên màu
        public virtual ICollection<ProductVariant> ProductVariants { get; set; } = new List<ProductVariant>();

    }

}
