using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;   // thêm dòng này
namespace DATN1API.Models;
public partial class Category
{
    public int CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public string? CategoryImage { get; set; }
    [JsonIgnore]
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
