namespace DATN1API.Models.ViewModels
{
    public class ProfileViewModel
    {
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Position { get; set; }
        public string AvatarUrl { get; set; } = "/hinh/logo.jpg";

    }

}
