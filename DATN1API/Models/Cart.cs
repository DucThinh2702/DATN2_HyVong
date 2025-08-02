using DATN1WEB.Models;
using System;
using System.Collections.Generic;

namespace DATN1API.Models;

public partial class Cart
{
    public int CartId { get; set; }

    public int? UserId { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? LastUpdated { get; set; }

    public virtual ICollection<CartDetail> CartDetails { get; set; } = new List<CartDetail>();

    public virtual ApplicationUser? User { get; set; } // Đảm bảo là ApplicationUser hoặc User

}
