using DATN1WEB.Models;
using Microsoft.AspNetCore.Identity;

public static class SeedRoles
{
    public static async Task CreateRoles(IServiceProvider serviceProvider, UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager)
    {
        string[] roleNames = { "Admin", "User" };  // Các vai trò bạn muốn tạo
        foreach (var roleName in roleNames)
        {
            var roleExist = await roleManager.RoleExistsAsync(roleName);
            if (!roleExist)
            {
                await roleManager.CreateAsync(new ApplicationRole(roleName));
            }
        }

        // Sau khi tạo roles, có thể tạo một admin mặc định
        var defaultAdmin = await userManager.FindByEmailAsync("admin@example.com");
        if (defaultAdmin == null)
        {
            var adminUser = new ApplicationUser
            {
                UserName = "admin@example.com",
                Email = "admin@example.com",
                FullName = "Admin User",
            };
            var result = await userManager.CreateAsync(adminUser, "YourAdminPassword123!");

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }
    }
}
