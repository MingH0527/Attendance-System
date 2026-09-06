using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using WebAndMobile.Models;

namespace WebAndMobile.Filters;

public class RequireStudentProfileAttribute : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        ClaimsPrincipal user = context.HttpContext.User;

        string? userId =
            user.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

        string? role =
            user.FindFirstValue(
                ClaimTypes.Role
            );

        // Not logged in
        if (string.IsNullOrWhiteSpace(userId))
        {
            context.Result =
                new RedirectToActionResult(
                    "Login",
                    "Account",
                    null
                );

            return;
        }

        // Only check Student
        if (!string.Equals(
                role,
                "Student",
                StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        DB db =
            context.HttpContext
                .RequestServices
                .GetRequiredService<DB>();

        bool profileCompleted =
            await db.StudentClasses
                .AsNoTracking()
                .AnyAsync(
                    sc => sc.UserId == userId
                );

        if (!profileCompleted)
        {
            if (context.Controller is Controller controller)
            {
                controller.TempData["ProfileRequired"] = "Required";
            }

            context.Result =
                new RedirectToActionResult(
                    "RegisterProgram",
                    "Program",
                    null
                );

            return;
        }
        await next();
    }
}