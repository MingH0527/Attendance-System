using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAndMobile.Models;
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
                    Avatar = u.Avatar
                })
                .ToListAsync();

        return View(users);
    }

    // GET: /User/Edit/5
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
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
        int id,
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

        // Guard: don't let the last Admin get demoted by accident.
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

        // Only touch the fields this screen is allowed to edit.
        // Address and Password are never bound or overwritten here.
        user.Username = model.Username.Trim();
        user.Email = model.Email.Trim();
        user.Phone = model.Phone.Trim();
        user.Gender = model.Gender;
        user.Position = model.Position;
        user.Status = model.Status;

        await db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // POST: /User/Delete/5  (soft delete -> Status = Inactive)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        User? user =
            await db.Users.FindAsync(id);

        if (user == null)
        {
            return NotFound(
                "User not found."
            );
        }

        if (user.Position == "Admin")
        {
            int adminCount =
                await db.Users.CountAsync(
                    u => u.Position == "Admin"
                );

            if (adminCount <= 1)
            {
                return BadRequest(
                    "Cannot deactivate the only " +
                    "remaining Admin."
                );
            }
        }

        user.Status = "Inactive";

        await db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // POST: /User/Activate/5  (reverse of soft delete)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id)
    {
        User? user =
            await db.Users.FindAsync(id);

        if (user == null)
        {
            return NotFound(
                "User not found."
            );
        }

        user.Status = "Active";

        await db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
}