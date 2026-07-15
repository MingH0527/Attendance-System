using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebAndMobile;
using WebAndMobile.Models;
using WebAndMobile.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

var databasePath = Path.Combine(
    builder.Environment.ContentRootPath,
    "DB.mdf"
);

var connectionString =
    $@"Server=(LocalDB)\MSSQLLocalDB;
       AttachDbFilename={databasePath};
       Initial Catalog=WebAndMobileDB;
       Integrated Security=True;
       MultipleActiveResultSets=True;
       TrustServerCertificate=True;";

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "The database connection string is empty."
    );
}

Console.WriteLine("Database path: " + databasePath);
Console.WriteLine("Connection string: " + connectionString);

builder.Services.AddDbContext<DB>(options =>
    options.UseSqlServer(connectionString)
);

builder.Services.AddScoped<
    IPasswordHasher<User>,
    PasswordHasher<User>
>();

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

/*
 * Cookie authentication, added for the Remember Me login feature.
 * IsPersistent / ExpiresUtc are set per-login inside
 * AccountController.Login based on the RememberMe checkbox; this
 * registration only supplies the shared defaults and cookie security
 * settings.
 */
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddScoped<GmailServiceHelper>();

builder.Services.AddScoped<
    ITimetableValidationService,
    TimetableValidationService
>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.Run();