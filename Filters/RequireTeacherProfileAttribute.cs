using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebAndMobile.Models;

namespace WebAndMobile.Filters;

public class RequireTeacherProfileAttribute : TypeFilterAttribute
{
    public RequireTeacherProfileAttribute()
        : base(typeof(RequireTeacherProfileFilter))
    {
    }
}


public class RequireTeacherProfileFilter : IAsyncActionFilter
{
    private readonly DB db;


    public RequireTeacherProfileFilter(DB db)
    {
        this.db = db;
    }


    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        // ==================================================
        // CURRENT USER
        // ==================================================

        ClaimsPrincipal user =
            context.HttpContext.User;


        if (
            user.Identity?.IsAuthenticated !=
            true
        )
        {
            await next();

            return;
        }


        // Only apply to Teacher
        if (!user.IsInRole("Teacher"))
        {
            await next();

            return;
        }


        // ==================================================
        // CURRENT ACTION
        // ==================================================

        string controllerName =
            context.RouteData
                .Values["controller"]
                ?.ToString()
            ?? string.Empty;


        string actionName =
            context.RouteData
                .Values["action"]
                ?.ToString()
            ?? string.Empty;


        // ==================================================
        // ALLOW PROFILE SETUP ACTIONS
        //
        // Create GET
        // Create POST
        // Subjects AJAX
        //
        // These MUST be allowed,
        // otherwise redirect loop will happen.
        // ==================================================

        bool isTeacherController =
            controllerName.Equals(
                "Teacher",
                StringComparison.OrdinalIgnoreCase
            );


        bool isProfileSetupAction =
            actionName.Equals(
                "Create",
                StringComparison.OrdinalIgnoreCase
            )
            ||
            actionName.Equals(
                "Subjects",
                StringComparison.OrdinalIgnoreCase
            );


        if (
            isTeacherController &&
            isProfileSetupAction
        )
        {
            await next();

            return;
        }


        // ==================================================
        // USER ID
        // ==================================================

        string? userId =
            user.FindFirstValue(
                ClaimTypes.NameIdentifier
            );


        if (
            string.IsNullOrWhiteSpace(
                userId
            )
        )
        {
            context.Result =
                new RedirectToActionResult(
                    "Login",
                    "Account",
                    null
                );

            return;
        }


        // ==================================================
        // CHECK TEACHER PROGRAM
        // ==================================================

        bool hasTeacherProgram =
            await db.TeacherPrograms
                .AsNoTracking()
                .AnyAsync(tp =>
                    tp.UserId ==
                    userId
                );


        // ==================================================
        // CHECK TEACHER COURSE / SUBJECT
        // ==================================================

        bool hasTeacherCourse =
            await db.TeacherCourses
                .AsNoTracking()
                .AnyAsync(tc =>
                    tc.UserId ==
                    userId
                );


        // ==================================================
        // PROFILE NOT COMPLETED
        // ==================================================

        if (
            !hasTeacherProgram ||
            !hasTeacherCourse
        )
        {
            context.Result =
                new RedirectToActionResult(
                    "Create",
                    "Teacher",
                    null
                );

            return;
        }


        // ==================================================
        // PROFILE COMPLETED
        // ==================================================

        await next();
    }
}