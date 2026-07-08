using Microsoft.AspNetCore.Mvc;

namespace WebAndMobile.Controllers;

public class AdminController : Controller
{
    public IActionResult Index()
    {
        return Content("Welcome Admin");
    }
}