using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Metrics;

namespace DATN1WEB.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [MaxLength(100)]
        public string? FullName { get; set; }

        [MaxLength(225)]
        public string? Address { get; set; }

        public DateTime? BirthDate { get; set; }

        [MaxLength(50)]
        public string? Gender { get; set; }

        public bool Status { get; set; } = false;  // False = Chưa xác thực, True = Đã xác thực
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

//ALTER TABLE AspNetUsers
//ADD CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
//UpdatedAt DATETIME2 NULL;
//Chạy lệnh này trong SQL để tạo thủ công nếu không sử dụng được lệnh Migration
    }
}
