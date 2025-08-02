namespace DATN1API.Models.ViewModels
{
    public class UpdateUserRoleViewModel
    {
        public string? UserId { get; set; } // ID của người dùng cần thay đổi vai trò
        public string? Role { get; set; } // Vai trò mới
    }
}
