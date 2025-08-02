using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

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
    }
}
