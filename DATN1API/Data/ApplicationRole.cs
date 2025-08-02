using Microsoft.AspNetCore.Identity;

namespace DATN1WEB.Models
{
    public class ApplicationRole : IdentityRole
    {
        public ApplicationRole() : base() { }

        public ApplicationRole(string roleName) : base(roleName) { }
        public string? Description { get; set; } // Mô tả vai trò

    }
}
