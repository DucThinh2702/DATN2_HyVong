using DATN1API.Models;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;   // thêm dòng này

namespace DATN1WEB.Models
{
    public class Size
    {
        [Key]
        public int SizeId { get; set; }    // public set
        public string? SizeName { get; set; }

        [JsonIgnore]
        public virtual ICollection<ProductVariant> ProductVariants { get; set; } = new List<ProductVariant>();
    }
}
