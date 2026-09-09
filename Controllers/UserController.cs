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


    // ==================================================
    // USER INDEX
    // ==================================================

    // GET: /User/Index
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        List<UserListItemDto> users =
            await db.Users
                .AsNoTracking()
                .OrderBy(u =>
                    u.Username
                )
                .Select(u =>
                    new UserListItemDto
                    {
                        UserId =
                            u.UserId,

                        Username =
                            u.Username,

                        Email =
                            u.Email,

                        Phone =
                            u.Phone,

                        Address =
                            u.Address,

                        Gender =
                            u.Gender,

                        Position =
                            u.Position,

                        Status =
                            u.Status,

                        Avatar =
                            u.Avatar,

                        IsDeleted =
                            u.IsDeleted,

                        DeletedAt =
                            u.DeletedAt
                    }
                )
                .ToListAsync();



        // ==================================================
        // STUDENT PROGRAM REGISTRATION
        // ==================================================

        Dictionary<string, StudentClass> studentClasses =
            await db.StudentClasses
                .AsNoTracking()
                .Include(sc =>
                    sc.Program
                )
                .Include(sc =>
                    sc.ClassGroup
                )
                .ToDictionaryAsync(
                    sc =>
                        sc.UserId
                );



        // ==================================================
        // TEACHER PROGRAM REGISTRATION
        // ==================================================

        List<TeacherProgram> teacherPrograms =
            await db.TeacherPrograms
                .AsNoTracking()
                .Include(tp =>
                    tp.Program
                )
                .ToListAsync();



        // ==================================================
        // BUILD USER LIST
        // ==================================================

        foreach (
            UserListItemDto user
            in users
        )
        {
            // ==============================================
            // STUDENT
            // ==============================================

            if (
                user.Position ==
                    "Student" &&
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
                user.Position ==
                    "Teacher"
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
                    registeredPrograms.Count >
                    0
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
                                    tp.Program !=
                                    null
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
                .Select(p =>
                    new
                    {
                        p.ProgramId,
                        p.ProgramName
                    }
                )
                .ToListAsync();



        ViewBag.AvailableClasses =
            await db.ClassGroups
                .AsNoTracking()
                .OrderBy(c =>
                    c.ClassName
                )
                .Select(c =>
                    new
                    {
                        c.ClassId,
                        c.ClassName,
                        c.ProgramId
                    }
                )
                .ToListAsync();



        return View(
            users
        );
    }



    // ==================================================
    // EDIT USER
    // ==================================================

    // GET: /User/Edit/5
    [HttpGet]
    public async Task<IActionResult> Edit(
        string id)
    {
        User? user =
            await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    u =>
                        u.UserId ==
                        id
                );


        if (
            user ==
            null
        )
        {
            return NotFound(
                "User not found."
            );
        }


        UserEditViewModel model =
            new()
            {
                UserId =
                    user.UserId,

                Username =
                    user.Username,

                Email =
                    user.Email,

                Phone =
                    user.Phone,

                Gender =
                    user.Gender,

                Position =
                    user.Position,

                Status =
                    user.Status,
            };


        return View(
            model
        );
    }



    // ==================================================
    // UPDATE USER
    // ==================================================

    // POST: /User/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string id,
        UserEditViewModel model)
    {
        if (
            id !=
            model.UserId
        )
        {
            return BadRequest(
                "Mismatched user id."
            );
        }


        if (
            !ModelState.IsValid
        )
        {
            return View(
                model
            );
        }


        User? user =
            await db.Users
                .FindAsync(
                    id
                );


        if (
            user ==
            null
        )
        {
            return NotFound(
                "User not found."
            );
        }


        // ==================================================
        // PROTECT LAST ADMIN
        // ==================================================

        if (
            user.Position ==
                "Admin" &&
            model.Position !=
                "Admin"
        )
        {
            int adminCount =
                await db.Users
                    .CountAsync(u =>
                        u.Position ==
                        "Admin"
                    );


            if (
                adminCount <=
                1
            )
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Cannot change the role of the " +
                    "only remaining Admin."
                );


                return View(
                    model
                );
            }
        }


        // ==================================================
        // UPDATE
        // ==================================================

        user.Username =
            model.Username.Trim();


        user.Email =
            model.Email.Trim();


        user.Phone =
            model.Phone.Trim();


        user.Gender =
            model.Gender;


        user.Position =
            model.Position;


        user.Status =
            model.Status;


        await db.SaveChangesAsync();


        return RedirectToAction(
            nameof(Index)
        );
    }



    // ==================================================
    // DELETE USER
    // ==================================================

    // POST: /User/Delete/5
    // Soft delete only:
    // the User row and linked historical records
    // are retained.

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        string id)
    {
        User? user =
            await db.Users
                .FindAsync(
                    id
                );


        if (
            user ==
            null
        )
        {
            return NotFound(
                "User not found."
            );
        }


        if (
            user.IsDeleted
        )
        {
            TempData["ErrorMessage"] =
                "This account has already been deleted.";


            return RedirectToAction(
                nameof(Index)
            );
        }


        // ==================================================
        // DO NOT DELETE LAST ADMIN
        // ==================================================

        if (
            user.Position ==
            "Admin"
        )
        {
            int activeAdminCount =
                await db.Users
                    .CountAsync(u =>
                        u.Position ==
                            "Admin" &&
                        !u.IsDeleted
                    );


            if (
                activeAdminCount <=
                1
            )
            {
                TempData["ErrorMessage"] =
                    "Cannot delete the only remaining Admin account.";


                return RedirectToAction(
                    nameof(Index)
                );
            }
        }


        // ==================================================
        // SOFT DELETE
        // ==================================================

        user.IsDeleted =
            true;


        user.DeletedAt =
            DateTime.Now;


        user.Status =
            "Deleted";


        await db.SaveChangesAsync();


        TempData["SuccessMessage"] =
            $"{user.Username} was deleted. " +
            "Historical attendance, payment and chat records were kept.";


        return RedirectToAction(
            nameof(Index)
        );
    }



    // ==================================================
    // BLOCK USER
    // ==================================================

    // POST: /User/Block/5

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Block(
        string id)
    {
        User? user =
            await db.Users
                .FindAsync(
                    id
                );


        if (
            user == null ||
            user.IsDeleted
        )
        {
            return NotFound(
                "User not found."
            );
        }


        if (
            user.Position ==
            "Admin"
        )
        {
            return BadRequest(
                "Admin accounts cannot be blocked here."
            );
        }


        user.Status =
            "Blocked";


        await db.SaveChangesAsync();


        return RedirectToAction(
            nameof(Index)
        );
    }



    // ==================================================
    // ACTIVATE USER
    // ==================================================

    // POST: /User/Activate/5

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(
        string id)
    {
        User? user =
            await db.Users
                .FindAsync(
                    id
                );


        if (
            user ==
            null
        )
        {
            return NotFound(
                "User not found."
            );
        }


        if (
            user.IsDeleted
        )
        {
            return BadRequest(
                "Deleted accounts cannot be restored " +
                "from the normal user list."
            );
        }


        user.Status =
            "Active";


        await db.SaveChangesAsync();


        return RedirectToAction(
            nameof(Index)
        );
    }



    // ==================================================
    // REGISTER STUDENT PROGRAM + CLASS
    // ==================================================

    // POST: /User/RegisterProgram

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterProgram(
        string userId,
        string programId,
        string classId)
    {
        // ==================================================
        // VALIDATION
        // ==================================================

        if (
            string.IsNullOrWhiteSpace(
                userId
            ) ||
            string.IsNullOrWhiteSpace(
                programId
            ) ||
            string.IsNullOrWhiteSpace(
                classId
            )
        )
        {
            TempData["ErrorMessage"] =
                "Please select both a program and a class.";


            return RedirectToAction(
                nameof(Index)
            );
        }



        // ==================================================
        // GET STUDENT
        // ==================================================

        User? user =
            await db.Users
                .FindAsync(
                    userId
                );


        if (
            user == null ||
            user.IsDeleted ||
            user.Position !=
                "Student"
        )
        {
            return NotFound(
                "Student not found."
            );
        }



        // ==================================================
        // VALIDATE PROGRAM
        // ==================================================

        bool programExists =
            await db.AcademicPrograms
                .AsNoTracking()
                .AnyAsync(p =>
                    p.ProgramId ==
                    programId
                );


        if (
            !programExists
        )
        {
            TempData["ErrorMessage"] =
                "Selected program was not found.";


            return RedirectToAction(
                nameof(Index)
            );
        }



        // ==================================================
        // VALIDATE CLASS
        // ==================================================

        ClassGroup? selectedClass =
            await db.ClassGroups
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.ClassId ==
                    classId
                );


        if (
            selectedClass ==
            null
        )
        {
            TempData["ErrorMessage"] =
                "Selected class was not found.";


            return RedirectToAction(
                nameof(Index)
            );
        }


        // Class must belong to selected Program

        if (
            selectedClass.ProgramId !=
            programId
        )
        {
            TempData["ErrorMessage"] =
                "The selected class does not belong " +
                "to the selected program.";


            return RedirectToAction(
                nameof(Index)
            );
        }



        // ==================================================
        // CURRENT STUDENT CLASS
        // ==================================================

        StudentClass? existing =
            await db.StudentClasses
                .FirstOrDefaultAsync(sc =>
                    sc.UserId ==
                    userId
                );



        // ==================================================
        // REGISTRATION DATE
        // ==================================================

        DateTime registeredAt =
            DateTime.Now;


        bool registrationChanged =
            false;



        // ==================================================
        // UPDATE EXISTING REGISTRATION
        // ==================================================

        if (
            existing !=
            null
        )
        {
            registrationChanged =
                existing.ProgramId !=
                    programId ||
                existing.ClassId !=
                    classId;


            existing.ProgramId =
                programId;


            existing.ClassId =
                classId;


            // Only reset registration date
            // when Program / Class really changes.

            if (
                registrationChanged
            )
            {
                existing.RegisteredDate =
                    registeredAt;
            }
            else
            {
                // Same class selected again.
                // Keep original RegisteredDate.

                registeredAt =
                    existing.RegisteredDate;
            }
        }


        // ==================================================
        // CREATE NEW STUDENT REGISTRATION
        // ==================================================

        else
        {
            string newStudentId =
                await GenerateStudentIdAsync();


            StudentClass studentClass =
                new()
                {
                    StudentClassId =
                        await FormattedIdGenerator
                            .NextAsync(
                                db.StudentClasses,
                                sc =>
                                    sc.StudentClassId,
                                "STC"
                            ),

                    UserId =
                        userId,

                    StudentId =
                        newStudentId,

                    ProgramId =
                        programId,

                    ClassId =
                        classId,

                    RegisteredDate =
                        registeredAt
                };


            db.StudentClasses
                .Add(
                    studentClass
                );


            registrationChanged =
                true;
        }



        // ==================================================
        // SAVE STUDENT CLASS FIRST
        // ==================================================

        await db.SaveChangesAsync();



        // ==================================================
        // BACKFILL RECENT TIMETABLE CHANGES
        // ==================================================
        //
        // Example:
        //
        // Admin cancelled lesson on 8 Sep.
        // Student joined this class on 9 Sep.
        // Cancelled lesson is on 10 Sep.
        //
        // Student should still know that the
        // 10 Sep lesson is cancelled.
        //
        // ==================================================

        await BackfillRecentTimetableNotificationsAsync(
            userId,
            classId,
            registeredAt
        );



        // ==================================================
        // SUCCESS
        // ==================================================

        TempData["SuccessMessage"] =
            registrationChanged
                ? $"{user.Username} has been registered to the selected program."
                : $"{user.Username}'s registration is already up to date.";


        return RedirectToAction(
            nameof(Index)
        );
    }



    // ==================================================
    // BACKFILL RECENT TIMETABLE NOTIFICATIONS
    // ==================================================

    private async Task
        BackfillRecentTimetableNotificationsAsync(
            string userId,
            string classId,
            DateTime registeredAt)
    {
        // ==================================================
        // ACTIVE SEMESTER
        // ==================================================

        AcademicTerm? activeTerm =
            await db.AcademicTerms
                .AsNoTracking()
                .Where(t =>
                    t.Status ==
                    "Active"
                )
                .OrderBy(t =>
                    t.StartDate
                )
                .FirstOrDefaultAsync();


        if (
            activeTerm ==
            null
        )
        {
            return;
        }



        // ==================================================
        // TIMETABLES FOR THIS CLASS
        // ==================================================

        List<string> timetableIds =
            await db.Timetables
                .AsNoTracking()
                .Where(t =>
                    t.TermId ==
                        activeTerm.TermId &&
                    t.ClassId ==
                        classId
                )
                .Select(t =>
                    t.TimetableId
                )
                .ToListAsync();


        if (
            timetableIds.Count ==
            0
        )
        {
            return;
        }



        // ==================================================
        // RECENT WINDOW
        // Same as Student Timetable:
        // last 7 days.
        // ==================================================

        DateTime notificationCutOff =
            DateTime.Now
                .AddDays(
                    -7
                );



        // ==================================================
        // FIND EXISTING PERSONAL NOTIFICATIONS
        // ==================================================
        //
        // TimetableController already creates
        // notifications separately for:
        //
        // - Teacher
        // - Student A
        // - Student B
        //
        // We can use any one of those copies as
        // the source for the new Student.
        //
        // ==================================================

        List<TimetableNotification> sourceNotifications =
            await db.TimetableNotifications
                .AsNoTracking()
                .Where(n =>

                    // Must be a personal notification.
                    // Ignore Admin/system notifications.
                    n.UserId !=
                        null &&

                    // Do not use this student's own
                    // notifications as source.
                    n.UserId !=
                        userId &&

                    // Active semester only.
                    n.TermId ==
                        activeTerm.TermId &&

                    // Must belong to this class timetable.
                    n.TimetableId !=
                        null &&

                    timetableIds.Contains(
                        n.TimetableId
                    ) &&

                    // Recent timetable changes only.
                    n.CreatedAt >=
                        notificationCutOff &&

                    // If occurrence date exists,
                    // do not give the student changes
                    // for lessons before they joined.
                    (
                        !n.OccurrenceDate.HasValue ||

                        n.OccurrenceDate
                            .Value
                            .Date >=
                        registeredAt.Date
                    )
                )
                .OrderByDescending(n =>
                    n.CreatedAt
                )
                .ToListAsync();



        if (
            sourceNotifications.Count ==
            0
        )
        {
            return;
        }



        // ==================================================
        // REMOVE DUPLICATES
        // ==================================================
        //
        // Example:
        //
        // Same LessonCancelled exists for:
        // Teacher
        // Student A
        // Student B
        //
        // New Student only needs ONE copy.
        //
        // ==================================================

        List<TimetableNotification> uniqueChanges =
            sourceNotifications
                .GroupBy(n =>
                    new
                    {
                        n.TimetableId,

                        OccurrenceDate =
                            n.OccurrenceDate
                                .HasValue
                                    ? n.OccurrenceDate
                                        .Value
                                        .Date
                                    : (DateTime?)null,

                        n.Type,

                        n.Message
                    }
                )
                .Select(g =>
                    g.OrderByDescending(n =>
                        n.CreatedAt
                    )
                    .First()
                )
                .OrderBy(n =>
                    n.CreatedAt
                )
                .ToList();



        // ==================================================
        // COPY TO NEW STUDENT
        // ==================================================

        foreach (
            TimetableNotification source
            in uniqueChanges
        )
        {
            // ==============================================
            // CHECK IF THIS STUDENT ALREADY HAS IT
            // ==============================================

            bool alreadyExists =
                await db.TimetableNotifications
                    .AsNoTracking()
                    .AnyAsync(n =>

                        n.UserId ==
                            userId &&

                        n.TimetableId ==
                            source.TimetableId &&

                        n.OccurrenceDate ==
                            source.OccurrenceDate &&

                        n.Type ==
                            source.Type &&

                        n.Message ==
                            source.Message
                    );


            if (
                alreadyExists
            )
            {
                continue;
            }



            // ==============================================
            // CREATE COPY
            // ==============================================

            TimetableNotification notification =
                new()
                {
                    NotificationId =
                        await FormattedIdGenerator
                            .NextAsync(
                                db.TimetableNotifications,
                                n =>
                                    n.NotificationId,
                                "NTF"
                            ),

                    UserId =
                        userId,

                    Type =
                        source.Type,

                    Message =
                        source.Message,

                    TermId =
                        source.TermId,

                    TimetableId =
                        source.TimetableId,

                    OccurrenceDate =
                        source.OccurrenceDate,

                    IsRead =
                        false,

                    // IMPORTANT:
                    // Keep the original date/time of
                    // the timetable change.
                    CreatedAt =
                        source.CreatedAt
                };


            db.TimetableNotifications
                .Add(
                    notification
                );


            // Save each notification so that the next
            // generated NTF ID cannot duplicate this one.

            await db.SaveChangesAsync();
        }
    }



    // ==================================================
    // GENERATE STUDENT ID
    // ==================================================

    private async Task<string>
        GenerateStudentIdAsync()
    {
        int year =
            DateTime.Now.Year;


        string prefix =
            $"S{year}";


        int countThisYear =
            await db.StudentClasses
                .CountAsync(sc =>
                    sc.StudentId
                        .StartsWith(
                            prefix
                        )
                );


        return
            $"{prefix}" +
            $"{(countThisYear + 1):D3}";
    }
}