
using DATN1API.Data;
using DATN1WEB.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình DbContext cho ứng dụng và Identity
builder.Services.AddDbContext<DatnContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
           .EnableSensitiveDataLogging()); // Kích hoạt Sensitive Data Logging nếu cần

// Cấu hình Identity
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    // Các tùy chọn cấu hình cho Identity
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 1;
    options.SignIn.RequireConfirmedEmail = true; // Bật xác nhận email nếu cần
    options.SignIn.RequireConfirmedPhoneNumber = false;
})
.AddEntityFrameworkStores<DatnContext>()
.AddDefaultTokenProviders();

// Cấu hình session nếu bạn dùng OTP hoặc giữ thông tin tạm
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(10); // Thời gian hết hạn của session
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Cấu hình dịch vụ MVC
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Cấu hình các dịch vụ khác
builder.Services.AddRazorPages().AddViewOptions(options =>
{
    options.HtmlHelperOptions.ClientValidationEnabled = true;
});

// Cấu hình các dịch vụ middleware
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // Cấu hình HSTS cho môi trường production
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Đảm bảo sử dụng session để lưu OTP
app.UseSession();

// Sử dụng Routing và Middleware cho Authentication và Authorization
app.UseRouting();

// Thêm xác thực và phân quyền
app.UseAuthentication();  // Thêm middleware cho xác thực
app.UseAuthorization();   // Thêm middleware cho phân quyền

// Cấu hình các route cho Controller
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
