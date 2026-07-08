using Microsoft.AspNetCore.Mvc;

namespace WebAndMobile.Controllers;

public class TeacherController : Controller
{
    public IActionResult Index()
    {
        return Content("Welcome Teacher");
    }
}