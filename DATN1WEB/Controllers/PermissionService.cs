using DATN1API.Data;
using DATN1WEB.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace DATN1API.Services
{
    public class PermissionService
    {
        private readonly DatnContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PermissionService(DatnContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<bool> HasPermission(ApplicationUser user, string function, string action)
        {
            // SuperAdmin thì luôn có quyền
            if (user.Email == "nguyenducthinhcn2005@gmail.com")
                return true;

            var permission = await _context.AdminPermissions
                .FirstOrDefaultAsync(p => p.UserId == user.Id && p.FunctionName == function);

            if (permission == null) return false;

            return action switch
            {
                "Create" => permission.CanCreate,
                "Edit" => permission.CanEdit,
                "Delete" => permission.CanDelete,
                "View" => permission.CanView,
                _ => false
            };
        }
    }
}
