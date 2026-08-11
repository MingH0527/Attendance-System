using Fido2NetLib;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Stripe;
using WebAndMobile;
using WebAndMobile.Hubs;
using WebAndMobile.Models;
using WebAndMobile.Services;

var builder = WebApplication.CreateBuilder(args);

// ==================================================
// MVC / SIGNALR
// ==================================================

builder.Services.AddControllersWithViews();

builder.Services.AddSignalR();

builder.Services.AddSingleton<
    IUserIdProvider,
    SignalRUserIdProvider
>();

// ==================================================
// DATABASE
// ==================================================

var databasePath = Path.Combine(
    builder.Environment.ContentRootPath,
    "DB.mdf"
);

var connectionString =
    $@"Server=(LocalDB)\MSSQLLocalDB;
       AttachDbFilename={databasePath};
       Integrated Security=True;
       MultipleActiveResultSets=True;
       TrustServerCertificate=True;";

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "The database connection string is empty."
    );
}

Console.WriteLine(
    "Database path: " + databasePath
);

Console.WriteLine(
    "Connection string: " + connectionString
);

builder.Services.AddDbContext<DB>(
    options =>
        options.UseSqlServer(
            connectionString
        )
);

// ==================================================
// PASSWORD HASHERS
// ==================================================

builder.Services.AddScoped<
    IPasswordHasher<User>,
    PasswordHasher<User>
>();

builder.Services.AddScoped<
    IPasswordHasher<AdminSecurity>,
    PasswordHasher<AdminSecurity>
>();

builder.Services.AddScoped<
    IPasswordHasher<PaymentSecurity>,
    PasswordHasher<PaymentSecurity>
>();

// ==================================================
// FACE RECOGNITION
// ==================================================

builder.Services.AddSingleton<
    IFaceRecognitionService,
    FaceRecognitionService
>();

// ==================================================
// SESSION
// ==================================================

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout =
        TimeSpan.FromMinutes(30);

    options.Cookie.HttpOnly =
        true;

    options.Cookie.IsEssential =
        true;

    options.Cookie.SameSite =
        SameSiteMode.Lax;

    options.Cookie.SecurePolicy =
        CookieSecurePolicy.SameAsRequest;
});

// ==================================================
// STRIPE
// ==================================================

StripeConfiguration.ApiKey =
    builder.Configuration[
        "Stripe:SecretKey"
    ];

// ==================================================
// COOKIE AUTHENTICATION
// ==================================================

builder.Services
    .AddAuthentication(
        CookieAuthenticationDefaults
            .AuthenticationScheme
    )
    .AddCookie(options =>
    {
        options.LoginPath =
            "/Account/Login";

        options.AccessDeniedPath =
            "/Account/AccessDenied";

        options.ExpireTimeSpan =
            TimeSpan.FromDays(30);

        options.SlidingExpiration =
            true;

        options.Cookie.HttpOnly =
            true;

        options.Cookie.SecurePolicy =
            CookieSecurePolicy.Always;

        options.Cookie.SameSite =
            SameSiteMode.Lax;
    });

// ==================================================
// AUTHORIZATION
// ==================================================

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        "AdminPasskeyAndPin",
        policy =>
        {
            policy.RequireAuthenticatedUser();

            policy.RequireRole(
                "Admin"
            );

            policy.RequireClaim(
                "PasskeyVerified",
                "true"
            );

            policy.RequireClaim(
                "AdminPinVerified",
                "true"
            );
        }
    );
});

// ==================================================
// ORIGINAL FIDO2 CONFIG
// ==================================================
//
// localhost FIDO2 configuration。
// 新版 MyProfileController / PasskeyController
// may use PasskeyFido2Factory to build a dunamic configuration。
// to avoid anothe fucntion depend on IFido2。
//

builder.Services.AddFido2(options =>
{
    options.ServerDomain =
        "localhost";

    options.ServerName =
        "Attendance System";

    options.Origins =
        new HashSet<string>
        {
            "https://localhost:7279"
        };
});

// ==================================================
// DYNAMIC PASSKEY CONFIGURATION
// ==================================================
//
// Private:
// localhost
//
// Public:
// Cloudflare / custom HTTPS domain
//

builder.Services.AddSingleton<
    PasskeyFido2Factory
>();

// ==================================================
// OTHER SERVICES
// ==================================================

builder.Services.AddScoped<
    GmailServiceHelper
>();

builder.Services.AddScoped<
    ITimetableValidationService,
    TimetableValidationService
>();

// ==================================================
// BUILD APPLICATION
// ==================================================

var app = builder.Build();

// ==================================================
// CLOUDFLARE / REVERSE PROXY HEADERS
// ==================================================
//
// need to put before UseHttpsRedirection() 
// so the PasskeyFido2Factory will know the correct
// Scheme / Host / Origin。
//

app.UseForwardedHeaders(
    new ForwardedHeadersOptions
    {
        ForwardedHeaders =
            ForwardedHeaders.XForwardedFor |
            ForwardedHeaders.XForwardedProto |
            ForwardedHeaders.XForwardedHost
    }
);

// ==================================================
// HTTP PIPELINE
// ==================================================

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

// Session must before the Controller running, otherwise the session will be null in the controller.
app.UseSession();

app.UseAuthentication();

app.UseAuthorization();

// ==================================================
// SIGNALR
// ==================================================

app.MapHub<ChatHub>(
    "/chatHub"
);

// ==================================================
// MVC ROUTE
// ==================================================

app.MapControllerRoute(
    name: "default",
    pattern:
        "{controller=Home}/{action=Index}/{id?}"
);

app.Run();