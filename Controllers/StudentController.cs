using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAndMobile.Controllers;

[Authorize]
public class StudentController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View("Student");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        HttpContext.Session.Clear();

        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme
        );

        return RedirectToAction(
            "Login",
            "Account"
        );
    }
}