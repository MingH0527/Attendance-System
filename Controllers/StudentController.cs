using Microsoft.AspNetCore.Mvc;

namespace WebAndMobile.Controllers;

public class StudentController : Controller
{
    public IActionResult Index()
    {
        return Content("Welcome Student");
    }
}