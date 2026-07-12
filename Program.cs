using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebAndMobile;
using WebAndMobile.Models;


var builder =
    WebApplication.CreateBuilder(
        args
    );

builder.Services
    .AddControllersWithViews();

builder.Services.AddDbContext<DB>(
    options =>
        options.UseSqlServer(
            builder.Configuration
                .GetConnectionString(
                    "DefaultConnection"
                )
        )
);

builder.Services.AddScoped<
    IPasswordHasher<User>,
    PasswordHasher<User>
>();

builder.Services.AddScoped<
    GmailServiceHelper
>();

var app =
    builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(
        "/Home/Error"
    );

    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapControllerRoute(
    name: "default",
    pattern:
        "{controller=Home}/{action=Index}/{id?}"
);

app.Run();