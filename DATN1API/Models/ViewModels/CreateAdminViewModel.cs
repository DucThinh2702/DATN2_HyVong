namespace DATN1API.Models.ViewModels
{
    public class CreateAdminViewModel
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Password { get; set; }
        public string Address { get; set; }
        public string Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }

        public List<PermissionDto> Permissions { get; set; } = new();

    }

}
