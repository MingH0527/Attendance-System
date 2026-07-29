using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAndMobile.Models;

namespace WebAndMobile.Controllers
{
    public class ProgramController : Controller
    {
        // TODO: 换成你项目实际的 DbContext 类名 (从截图看你的文件叫 DB.cs, 类名可能是 DB 或类似的, 请确认并替换)
        private readonly DB _context;

        public ProgramController(DB context)
        {
            _context = context;
        }

        // GET: /Program/RegisterProgram
        [HttpGet]
        public async Task<IActionResult> RegisterProgram()
        {
            // TODO: 换成你项目实际读取登录用户的方式 (Session / Claims)
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = new ProgramRegisterViewModel
            {
                Position = user.Position,
                AvailableClasses = await _context.ClassGroups.AsNoTracking().ToListAsync(),
                AvailableCourses = await _context.Courses.AsNoTracking().ToListAsync(),
            };

            if (user.Position == "Student")
            {
                var existing = await _context.StudentClasses
                    .Include(sc => sc.ClassGroup)
                    .FirstOrDefaultAsync(sc => sc.UserId == userId.Value);

                if (existing != null)
                {
                    model.SelectedClassId = existing.ClassId;
                    model.CurrentClassName = existing.ClassGroup?.ClassName;
                }
            }
            else if (user.Position == "Teacher")
            {
                var existing = await _context.TeacherCourses
                    .Include(tc => tc.Course)
                    .Where(tc => tc.UserId == userId.Value)
                    .ToListAsync();

                model.SelectedCourseIds = existing.Select(tc => tc.CourseId).ToList();
                model.CurrentCourseNames = existing.Select(tc => tc.Course!.CourseName).ToList();
            }

            return View(model);
        }

        // POST: /Program/RegisterProgram
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterProgram(ProgramRegisterViewModel model)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (user.Position == "Student" && model.SelectedClassId == null)
            {
                ModelState.AddModelError(nameof(model.SelectedClassId), "请选择班级");
            }

            if (user.Position == "Teacher" && !model.SelectedCourseIds.Any())
            {
                ModelState.AddModelError(nameof(model.SelectedCourseIds), "请至少选择一门课程");
            }

            if (!ModelState.IsValid)
            {
                model.Position = user.Position;
                model.AvailableClasses = await _context.ClassGroups.AsNoTracking().ToListAsync();
                model.AvailableCourses = await _context.Courses.AsNoTracking().ToListAsync();
                return View(model);
            }

            if (user.Position == "Student")
            {
                // upsert: 一个学生同一时间只属于一个班, 有旧记录就更新, 没有就新增
                var existing = await _context.StudentClasses
                    .FirstOrDefaultAsync(sc => sc.UserId == userId.Value);

                if (existing != null)
                {
                    existing.ClassId = model.SelectedClassId!.Value;
                    existing.RegisteredDate = DateTime.Now;
                }
                else
                {
                    _context.StudentClasses.Add(new StudentClass
                    {
                        UserId = userId.Value,
                        ClassId = model.SelectedClassId!.Value
                    });
                }
            }
            else if (user.Position == "Teacher")
            {
                // 先移除旧的任教记录, 再按这次勾选的重新加入
                var oldRecords = _context.TeacherCourses.Where(tc => tc.UserId == userId.Value);
                _context.TeacherCourses.RemoveRange(oldRecords);

                foreach (var courseId in model.SelectedCourseIds)
                {
                    _context.TeacherCourses.Add(new TeacherCourse
                    {
                        UserId = userId.Value,
                        CourseId = courseId
                    });
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Registration saved successfully.";
            return RedirectToAction("MyProfile", "Profile"); // TODO: 换成你实际 MyProfile 所在的 Controller 名
        }
    }
}