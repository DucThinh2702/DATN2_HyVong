using DATN1API.Data;
using DATN1API.Models.Pay;
using DATN1API.Pay;
using DATN1API.Services;
using DATN1WEB.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ===== DB + Identity =====
builder.Services.AddDbContext<DatnContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
           .EnableSensitiveDataLogging());

builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 1;
    options.SignIn.RequireConfirmedEmail = true;
})
.AddEntityFrameworkStores<DatnContext>()
.AddDefaultTokenProviders();

// ⚠️ KHÔNG AddCookie lại cho IdentityConstants.ApplicationScheme
// Hãy cấu hình cookie USER mặc định qua ConfigureApplicationCookie:
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "UserAuthCookie";             // cookie cho USER
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/Login";
});

// ===== AUTH tổng: PolicyScheme điều hướng theo ngữ cảnh =====
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "AppAuth";
    options.DefaultChallengeScheme = "AppAuth";
})
.AddPolicyScheme("AppAuth", "Combined Scheme", o =>
{
    o.ForwardDefaultSelector = ctx =>
    {
        // CHỈ theo path /Admin. Đừng auto chuyển theo sự tồn tại của cookie Admin.
        return ctx.Request.Path.StartsWithSegments("/Admin")
            ? "AdminScheme"
            : IdentityConstants.ApplicationScheme; // cookie user
    };
})

// Cookie riêng cho ADMIN
.AddCookie("AdminScheme", o =>
{
    o.Cookie.Name = "AdminAuthCookie";
    o.LoginPath = "/Account/Login";        // dùng chung form login
    o.AccessDeniedPath = "/Account/Login"; // có thể đổi sang trang báo lỗi riêng
});

// ===== Authorization: policy Admin bắt buộc cookie AdminScheme + role Admin =====
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("IsAdmin", p =>
    {
        p.AddAuthenticationSchemes("AdminScheme");
        p.RequireRole("Admin");
    });
});

// ===== Các service khác của bạn =====
builder.Services.Configure<PayOSOptions>(builder.Configuration.GetSection("PayOS"));
builder.Services.AddHttpClient<PayOSService>(client =>
{
    var opt = builder.Configuration.GetSection("PayOS").Get<PayOSOptions>();
    if (!string.IsNullOrWhiteSpace(opt?.BaseUrl))
        client.BaseAddress = new Uri(opt.BaseUrl.Trim());
    if (!string.IsNullOrEmpty(opt?.ClientId))
        client.DefaultRequestHeaders.Add("X-Client-Id", opt.ClientId);
    if (!string.IsNullOrEmpty(opt?.ApiKey))
        client.DefaultRequestHeaders.Add("X-Api-Key", opt.ApiKey);
});

builder.Services.Configure<VNPAYSettings>(builder.Configuration.GetSection("VNPAY"));
builder.Services.AddScoped<IVnPayService, VnPayService>();
builder.Services.AddScoped<VnPayService>();
builder.Services.AddScoped<PermissionService>();

builder.Services.AddSession(o =>
{
    o.IdleTimeout = TimeSpan.FromMinutes(10);
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
});

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient("api", client =>
{
    client.BaseAddress = new Uri("https://localhost:7138/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    string[] roles = { "SuperAdmin", "Admin", "User" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new ApplicationRole(role));
        }
    }

    // Gán SuperAdmin cho email đặc biệt
    var superAdminEmail = "nguyenducthinhcn2005@gmail.com";
    var superAdmin = await userManager.FindByEmailAsync(superAdminEmail);
    if (superAdmin != null && !await userManager.IsInRoleAsync(superAdmin, "SuperAdmin"))
    {
        await userManager.AddToRoleAsync(superAdmin, "SuperAdmin");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSession();
app.UseRouting();

// chống cache (tuỳ chọn)
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
    ctx.Response.Headers["Pragma"] = "no-cache";
    ctx.Response.Headers["Expires"] = "0";
    await next();
});
// --- NO-CACHE CHO NỘI DUNG CẦN ĐĂNG NHẬP (đặc biệt /Admin) ---
app.Use(async (ctx, next) =>
{
    // Nếu là khu Admin HOẶC người dùng đang đăng nhập => không cho cache
    if (ctx.Request.Path.StartsWithSegments("/Admin") ||
        (ctx.User?.Identity?.IsAuthenticated ?? false))
    {
        ctx.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0, private";
        ctx.Response.Headers["Pragma"] = "no-cache";
        ctx.Response.Headers["Expires"] = "0";
    }

    await next();

    // Nếu bị 401/403 (đã đăng xuất rồi bấm Back) => đảm bảo không có cache
    if (ctx.Response.StatusCode == StatusCodes.Status401Unauthorized ||
        ctx.Response.StatusCode == StatusCodes.Status403Forbidden)
    {
        ctx.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0, private";
        ctx.Response.Headers["Pragma"] = "no-cache";
        ctx.Response.Headers["Expires"] = "0";
    }
});

app.UseAuthentication();
app.UseAuthorization();

// Khu ADMIN: bắt buộc chính sách IsAdmin (cookie Admin + role Admin)
app.MapControllerRoute(
    name: "admin",
    pattern: "Admin/{action=Index}/{id?}",
    defaults: new { controller = "Admin" }
).RequireAuthorization("IsAdmin");

// Khu USER (mặc định) — KHÔNG trỏ vào Admin
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
