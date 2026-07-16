using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace WebAndMobile.Controllers;

public class StudentController : Controller
{
    // GET: /Student/Index
    [HttpGet]
    public IActionResult Index()
    {
        return View("Student");
    }

    // POST: /Student/Logout
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        // Clear session data.
        HttpContext.Session.Clear();

        // Remove the authentication cookie.
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme
        );

        // Return to login page.
        return RedirectToAction(
            "Login",
            "Account"
        );
    }
}