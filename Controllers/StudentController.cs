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
    public IActionResult Logout()
    {
        // clear the current loginSession
        HttpContext.Session.Clear();

        // back to login page
        return RedirectToAction("Login", "Account");
    }
}