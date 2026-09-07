using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAndMobile.Models;
using WebAndMobile.Services;
using WebAndMobile.ViewModels;

namespace WebAndMobile.Controllers;

[Authorize(Roles = "Admin")]
public class UserController : Controller
{
    private readonly DB db;

    public UserController(DB db)
    {
        this.db = db;
    }

    // GET: /User/Index
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        List<UserListItemDto> users =
            await db.Users
                .AsNoTracking()
                .OrderBy(u => u.Username)
                .Select(u => new UserListItemDto
                {
                    UserId = u.UserId,
                    Username = u.Username,
                    Email = u.Email,
                    Phone = u.Phone,
                    Address = u.Address,
                    Gender = u.Gender,
                    Position = u.Position,
                    Status = u.Status,
                    Avatar = u.Avatar,
                    IsDeleted = u.IsDeleted,
                    DeletedAt = u.DeletedAt
                })
                .ToListAsync();


        // ==================================================
        // STUDENT PROGRAM REGISTRATION
        // ==================================================

        Dictionary<string, StudentClass> studentClasses =
            await db.StudentClasses
                .AsNoTracking()
                .Include(sc => sc.Program)
                .Include(sc => sc.ClassGroup)
                .ToDictionaryAsync(
                    sc => sc.UserId
                );


        // ==================================================
        // TEACHER PROGRAM REGISTRATION
        // ==================================================

        List<TeacherProgram> teacherPrograms =
            await db.TeacherPrograms
                .AsNoTracking()
                .Include(tp => tp.Program)
                .ToListAsync();


        foreach (UserListItemDto user in users)
        {
            // ==============================================
            // STUDENT
            // ==============================================

            if (
                user.Position == "Student" &&
                studentClasses.TryGetValue(
                    user.UserId,
                    out StudentClass? sc
                )
            )
            {
                user.HasProgram =
                    true;

                user.ProgramId =
                    sc.ProgramId;

                user.ProgramName =
                    sc.Program?.ProgramName
                    ?? sc.ProgramId;

                user.ClassId =
                    sc.ClassId;

                user.ClassName =
                    sc.ClassGroup?.ClassName
                    ?? sc.ClassId;

                user.StudentId =
                    sc.StudentId;
            }


            // ==============================================
            // TEACHER
            // ==============================================

            else if (
                user.Position == "Teacher"
            )
            {
                List<TeacherProgram> registeredPrograms =
                    teacherPrograms
                        .Where(tp =>
                            tp.UserId ==
                            user.UserId
                        )
                        .ToList();


                // Teacher has registered program
                if (
                    registeredPrograms.Count > 0
                )
                {
                    user.HasProgram =
                        true;


                    // First ProgramId
                    // Mainly used by HTML data attribute
                    user.ProgramId =
                        registeredPrograms
                            .First()
                            .ProgramId;


                    // Show all programs registered by teacher
                    user.ProgramName =
                        string.Join(
                            "\n",
                            registeredPrograms
                                .Where(tp =>
                                    tp.Program != null
                                )
                                .Select(tp =>
                                    tp.Program!
                                        .ProgramName
                                )
                                .Distinct()
                        );


                    // Fallback if Program relationship
                    // cannot be loaded
                    if (
                        string.IsNullOrWhiteSpace(
                            user.ProgramName
                        )
                    )
                    {
                        user.ProgramName =
                            string.Join(
                                ", ",
                                registeredPrograms
                                    .Select(tp =>
                                        tp.ProgramId
                                    )
                                    .Distinct()
                            );
                    }
                }
                else
                {
                    // Teacher has not registered any program
                    user.HasProgram =
                        false;

                    user.ProgramId =
                        null;

                    user.ProgramName =
                        null;
                }
            }
        }


        // ==================================================
        // REGISTER PROGRAM MODAL DATA
        // ==================================================

        ViewBag.AvailablePrograms =
            await db.AcademicPrograms
                .AsNoTracking()
                .OrderBy(p =>
                    p.ProgramName
                )
                .Select(p => new
                {
                    p.ProgramId,
                    p.ProgramName
                })
                .ToListAsync();


        ViewBag.AvailableClasses =
            await db.ClassGroups
                .AsNoTracking()
                .OrderBy(c =>
                    c.ClassName
                )
                .Select(c => new
                {
                    c.ClassId,
                    c.ClassName,
                    c.ProgramId
                })
                .ToListAsync();


        return View(users);
    }

    // GET: /User/Edit/5
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        User? user =
            await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    u => u.UserId == id
                );

        if (user == null)
        {
            return NotFound(
                "User not found."
            );
        }

        UserEditViewModel model = new()
        {
            UserId = user.UserId,
            Username = user.Username,
            Email = user.Email,
            Phone = user.Phone,
            Gender = user.Gender,
            Position = user.Position,
            Status = user.Status,
        };

        return View(model);
    }

    // POST: /User/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string id,
        UserEditViewModel model)
    {
        if (id != model.UserId)
        {
            return BadRequest(
                "Mismatched user id."
            );
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        User? user =
            await db.Users.FindAsync(id);

        if (user == null)
        {
            return NotFound(
                "User not found."
            );
        }

        if (user.Position == "Admin" &&
            model.Position != "Admin")
        {
            int adminCount =
                await db.Users.CountAsync(
                    u => u.Position == "Admin"
                );

            if (adminCount <= 1)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Cannot change the role of the " +
                    "only remaining Admin."
                );

                return View(model);
            }
        }

        user.Username = model.Username.Trim();
        user.Email = model.Email.Trim();
        user.Phone = model.Phone.Trim();
        user.Gender = model.Gender;
        user.Position = model.Position;
        user.Status = model.Status;

        await db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // POST: /User/Delete/5
    // Soft delete only: the User row and all linked historical records are retained.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        User? user = await db.Users.FindAsync(id);

        if (user == null)
        {
            return NotFound("User not found.");
        }

        if (user.IsDeleted)
        {
            TempData["ErrorMessage"] = "This account has already been deleted.";
            return RedirectToAction(nameof(Index));
        }

        if (user.Position == "Admin")
        {
            int activeAdminCount = await db.Users.CountAsync(u =>
                u.Position == "Admin" && !u.IsDeleted);

            if (activeAdminCount <= 1)
            {
                TempData["ErrorMessage"] =
                    "Cannot delete the only remaining Admin account.";
                return RedirectToAction(nameof(Index));
            }
        }

        user.IsDeleted = true;
        user.DeletedAt = DateTime.Now;
        user.Status = "Deleted";

        await db.SaveChangesAsync();

        TempData["SuccessMessage"] =
            $"{user.Username} was deleted. Historical attendance, payment and chat records were kept.";

        return RedirectToAction(nameof(Index));
    }

    // POST: /User/Block/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Block(string id)
    {
        User? user = await db.Users.FindAsync(id);
        if (user == null || user.IsDeleted) return NotFound("User not found.");
        if (user.Position == "Admin") return BadRequest("Admin accounts cannot be blocked here.");

        user.Status = "Blocked";
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // POST: /User/Activate/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(string id)
    {
        User? user =
            await db.Users.FindAsync(id);

        if (user == null)
        {
            return NotFound(
                "User not found."
            );
        }

        if (user.IsDeleted)
        {
            return BadRequest("Deleted accounts cannot be restored from the normal user list.");
        }

        user.Status = "Active";

        await db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // POST: /User/RegisterProgram
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterProgram(
        string userId,
        string programId,
        string classId)
    {
        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(programId) ||
            string.IsNullOrWhiteSpace(classId))
        {
            TempData["ErrorMessage"] =
                "Please select both a program and a class.";

            return RedirectToAction(nameof(Index));
        }

        User? user = await db.Users.FindAsync(userId);

        if (user == null || user.IsDeleted || user.Position != "Student")
        {
            return NotFound("Student not found.");
        }

        StudentClass? existing =
            await db.StudentClasses
                .FirstOrDefaultAsync(sc => sc.UserId == userId);

        if (existing != null)
        {
            existing.ProgramId = programId;
            existing.ClassId = classId;
            existing.RegisteredDate = DateTime.Now;
        }
        else
        {
            string newStudentId = await GenerateStudentIdAsync();

            db.StudentClasses.Add(new StudentClass
            {
                StudentClassId = await FormattedIdGenerator.NextAsync(
                    db.StudentClasses,
                    sc => sc.StudentClassId,
                    "STC"
                ),
                UserId = userId,
                StudentId = newStudentId,
                ProgramId = programId,
                ClassId = classId
            });
        }

        await db.SaveChangesAsync();

        TempData["SuccessMessage"] =
            $"{user.Username} has been registered to the selected program.";

        return RedirectToAction(nameof(Index));
    }

    private async Task<string> GenerateStudentIdAsync()
    {
        int year = DateTime.Now.Year;
        string prefix = $"S{year}";

        int countThisYear = await db.StudentClasses
            .CountAsync(sc => sc.StudentId.StartsWith(prefix));

        return $"{prefix}{(countThisYear + 1):D3}";
    }
}