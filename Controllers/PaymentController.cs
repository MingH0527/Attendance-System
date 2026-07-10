
using Microsoft.AspNetCore.Mvc;
using WebAndMobile.Models;
 
namespace WebAndMobile.Controllers
{
    public class PaymentController : Controller
    {
        // TODO: 换成从数据库读取，这里先用写死的清单做示范
        private static readonly List<FeeItem> _fees = new()
        {
            new FeeItem { Id = 1, Name = "Semester Fee",     Description = "Tuition for current semester", Amount = 1200.00m, Category = "Tuition", AmountIsFixed = true },
            new FeeItem { Id = 2, Name = "Library Fine",     Description = "Overdue book fine",             Amount = 15.00m,   Category = "Library", AmountIsFixed = true },
            new FeeItem { Id = 3, Name = "Hostel Fee",       Description = "Monthly accommodation",         Amount = 800.00m,  Category = "Hostel",  AmountIsFixed = true },
            new FeeItem { Id = 4, Name = "Course Textbook",  Description = "Purchase required textbook(s)", Amount = 45.00m,   Category = "Books",   AmountIsFixed = false },
        };

        // GET: /Payment/SelectFee
        // 费用清单页 - 学生点选要缴什么
        [HttpGet]
        public IActionResult SelectFee()
        {
            return View(_fees);
        }

        // GET: /Payment/Pay?feeId=1
        // 确认缴费页 - 学生信息从登录状态取, 不用再填
        [HttpGet]
        public IActionResult Pay(int feeId)
        {
            var fee = _fees.FirstOrDefault(f => f.Id == feeId);
            if (fee == null)
            {
                return RedirectToAction("SelectFee");
            }

            // TODO: 换成你项目实际的登录信息读取方式，例如：
            //   - Session:      HttpContext.Session.GetString("StudentId")
            //   - Claims/Identity: User.FindFirst("StudentId")?.Value
            var studentId = HttpContext.Session.GetString("StudentId") ?? "";
            var studentName = HttpContext.Session.GetString("StudentName") ?? "";

            var model = new Payment
            {
                FeeId = fee.Id,
                FeeName = fee.Name,
                Amount = fee.Amount,
                StudentId = studentId,
                StudentName = studentName
            };

            return View(model);
        }

        // POST: /Payment/Pay
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Pay(Payment model)
        {
            // 安全起见：学号/姓名一律以服务器 session 为准，不信任表单里带回来的隐藏字段
            model.StudentId = HttpContext.Session.GetString("StudentId") ?? model.StudentId;
            model.StudentName = HttpContext.Session.GetString("StudentName") ?? model.StudentName;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // TODO: 在这里把 model 存进数据库 / 呼叫真正的付款处理

            return RedirectToAction("Success");
        }

        [HttpGet]
        public IActionResult Success()
        {
            return View();
        }
    }
}

