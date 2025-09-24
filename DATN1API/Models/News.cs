using DATN1WEB.Models;
using Microsoft.AspNetCore.Identity;  // Thêm namespace Identity nếu chưa có
using System;
using System.Collections.Generic;

namespace DATN1API.Models;

public partial class News
{
    public int NewsId { get; set; }

    public string? Title { get; set; }

    public string? Content { get; set; }

    public DateTime? PostedDate { get; set; }

    public string? AuthorId { get; set; }
    public virtual ApplicationUser? Author { get; set; }


    public string? ThumbnailImage { get; set; }

 
}



