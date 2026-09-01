using Fido2NetLib;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using Stripe;
using WebAndMobile;
using WebAndMobile.Hubs;
using WebAndMobile.Models;
using WebAndMobile.Services;

var builder = WebApplication.CreateBuilder(args);

// QuestPDF Community licence is suitable for this student project.
QuestPDF.Settings.License = LicenseType.Community;

// MVC / SIGNALR
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, SignalRUserIdProvider>();

// DATABASE
var databasePath = Path.Combine(builder.Environment.ContentRootPath, "DB.mdf");
var connectionString = $@"Server=(LocalDB)\MSSQLLocalDB;
       AttachDbFilename={databasePath};
       Integrated Security=True;
       MultipleActiveResultSets=True;
       TrustServerCertificate=True;";

if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("The database connection string is empty.");

Console.WriteLine("Database path: " + databasePath);
Console.WriteLine("Connection string: " + connectionString);
builder.Services.AddDbContext<DB>(options => options.UseSqlServer(connectionString));

// PASSWORD HASHERS
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IPasswordHasher<AdminSecurity>, PasswordHasher<AdminSecurity>>();
builder.Services.AddScoped<IPasswordHasher<PaymentSecurity>, PasswordHasher<PaymentSecurity>>();

// FACE RECOGNITION
builder.Services.AddSingleton<IFaceRecognitionService, FaceRecognitionService>();

// SESSION
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// STRIPE
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

// COOKIE AUTHENTICATION
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

// AUTHORIZATION
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminPasskeyAndPin", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Admin");
        policy.RequireClaim("PasskeyVerified", "true");
        policy.RequireClaim("AdminPinVerified", "true");
    });
});

// FIDO2
builder.Services.AddFido2(options =>
{
    options.ServerDomain = "localhost";
    options.ServerName = "Attendance System";
    options.Origins = new HashSet<string> { "https://localhost:7279" };
});
builder.Services.AddSingleton<PasskeyFido2Factory>();

// OTHER SERVICES
builder.Services.AddScoped<GmailServiceHelper>();
builder.Services.AddScoped<ReceiptPdfService>();
builder.Services.AddScoped<TimetablePdfService>();
builder.Services.AddScoped<ITimetableValidationService, TimetableValidationService>();

var app = builder.Build();

// CLOUDFLARE / REVERSE PROXY HEADERS
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                       ForwardedHeaders.XForwardedProto |
                       ForwardedHeaders.XForwardedHost
});

// HTTP PIPELINE
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// SIGNALR
app.MapHub<ChatHub>("/chatHub");

// MVC ROUTE
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
