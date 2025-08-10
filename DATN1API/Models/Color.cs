
using DATN1API.Models;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;   // thêm dòng này
namespace DATN1WEB.Models
{
    public class Color
    {
        [Key]
        public int ColorId { get; set; }    // Đổi internal set thành public set
        public string? ColorName { get; set; }  // Tên màu

        [JsonIgnore]
        public virtual ICollection<ProductVariant> ProductVariants { get; set; } = new List<ProductVariant>();
    }

}
