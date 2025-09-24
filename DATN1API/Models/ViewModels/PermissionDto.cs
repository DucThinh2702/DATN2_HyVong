namespace DATN1API.Models.ViewModels
{
    public class PermissionDto
    {
        public string FunctionName { get; set; } = "";
        public string DisplayName { get; set; } = ""; // tên tiếng Việt để hiển thị
        public bool CanView { get; set; }
        public bool CanCreate { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }

}
