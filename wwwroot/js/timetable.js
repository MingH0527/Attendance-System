(function () {
    "use strict";

    const HOURS = [9, 10, 11, 12, 13, 14, 15, 16]; // 9:00 - 17:00, 8 one-hour slots
    const SCHOOL_DAYS = [1, 2, 3, 4, 5];   // Monday .. Friday (JS/C# DayOfWeek: Sun=0)
    const TUITION_DAYS = [6, 0];           // Saturday, Sunday
    const DAY_NAMES = { 0: "Sunday", 1: "Monday", 2: "Tuesday", 3: "Wednesday", 4: "Thursday", 5: "Friday", 6: "Saturday" };

    const state = {
        classType: "School",
        classId: null,
        courses: [],
        classes: [],
        classrooms: [],
        entries: []
    };

    document.addEventListener("DOMContentLoaded", init);

    async function init() {
        bindTabs();
        bindToolbar();
        bindModal();
        bindLookupModal();

        await Promise.all([loadTeachers(), loadCourses(), loadClasses(), loadClassrooms()]);
        renderGridSkeleton();
        await loadGrid();
    }

    function bindTabs() {
        document.querySelectorAll(".tt-tab").forEach(tab => {
            tab.addEventListener("click", async () => {
                document.querySelectorAll(".tt-tab").forEach(t => t.classList.remove("active"));
                tab.classList.add("active");
                state.classType = tab.dataset.classtype;
                renderGridSkeleton();
                await loadGrid();
            });
        });
    }

    function bindToolbar() {
        document.getElementById("classFilter").addEventListener("change", async (e) => {
            state.classId = e.target.value ? parseInt(e.target.value, 10) : null;
            await loadGrid();
        });

        document.getElementById("manageLookupsBtn").addEventListener("click", () => {
            openLookupModal();
        });
    }

    // ------------------------------------------------------------------
    // Loading lookups
    // ------------------------------------------------------------------

    async function loadTeachers() {
        const res = await fetch("/Admin/GetTeachers");
        const teachers = await res.json();
        const list = document.getElementById("teacherList");
        list.innerHTML = "";
        teachers.forEach(t => {
            const li = document.createElement("li");
            li.textContent = t.teacherName;
            li.draggable = true;
            li.dataset.teacherId = t.teacherId;
            li.dataset.teacherName = t.teacherName;
            li.addEventListener("dragstart", (e) => {
                e.dataTransfer.setData("teacherId", t.teacherId);
                e.dataTransfer.setData("teacherName", t.teacherName);
            });
            list.appendChild(li);
        });
    }

    async function loadCourses() {
        const res = await fetch("/Admin/GetCourses");
        state.courses = await res.json();
        fillSelect(document.getElementById("fCourseId"), state.courses, "-- select course --");
    }

    async function loadClasses() {
        const res = await fetch("/Admin/GetClasses");
        state.classes = await res.json();
        fillSelect(document.getElementById("fClassId"), state.classes, "-- select class --");

        const filter = document.getElementById("classFilter");
        filter.innerHTML = '<option value="">All classes</option>';
        state.classes.forEach(c => {
            const opt = document.createElement("option");
            opt.value = c.id;
            opt.textContent = c.name;
            filter.appendChild(opt);
        });
    }

    async function loadClassrooms() {
        const res = await fetch("/Admin/GetClassrooms");
        state.classrooms = await res.json();
        const select = document.getElementById("fClassroomId");
        select.innerHTML = '<option value="">-- none --</option>';
        state.classrooms.forEach(c => {
            const opt = document.createElement("option");
            opt.value = c.id;
            opt.textContent = c.name;
            select.appendChild(opt);
        });
    }

    function fillSelect(select, items, placeholder) {
        select.innerHTML = `<option value="">${placeholder}</option>`;
        items.forEach(i => {
            const opt = document.createElement("option");
            opt.value = i.id;
            opt.textContent = i.name;
            select.appendChild(opt);
        });
    }

    // ------------------------------------------------------------------
    // Grid rendering
    // ------------------------------------------------------------------

    function currentDays() {
        return state.classType === "School" ? SCHOOL_DAYS : TUITION_DAYS;
    }

    function renderGridSkeleton() {
        const headerRow = document.getElementById("gridHeaderRow");
        headerRow.innerHTML = "<th>Day / Time</th>";
        HOURS.forEach(h => {
            const th = document.createElement("th");
            th.textContent = `${formatHour(h)} - ${formatHour(h + 1)}`;
            headerRow.appendChild(th);
        });

        const body = document.getElementById("gridBody");
        body.innerHTML = "";
        currentDays().forEach(day => {
            const row = document.createElement("tr");
            row.dataset.day = day;

            const label = document.createElement("td");
            label.className = "tt-day-label";
            label.textContent = DAY_NAMES[day];
            row.appendChild(label);

            HOURS.forEach(h => {
                const cell = document.createElement("td");
                cell.className = "tt-cell";
                cell.dataset.day = day;
                cell.dataset.hour = h;
                bindCellDragDrop(cell);
                row.appendChild(cell);
            });

            body.appendChild(row);
        });
    }

    function bindCellDragDrop(cell) {
        cell.addEventListener("dragover", (e) => {
            e.preventDefault();
            cell.classList.add("drag-over");
        });
        cell.addEventListener("dragleave", () => cell.classList.remove("drag-over"));
        cell.addEventListener("drop", (e) => {
            e.preventDefault();
            cell.classList.remove("drag-over");
            const teacherId = e.dataTransfer.getData("teacherId");
            const teacherName = e.dataTransfer.getData("teacherName");
            const day = parseInt(cell.dataset.day, 10);
            const hour = parseInt(cell.dataset.hour, 10);
            openModalForCreate(day, teacherId, teacherName, hour);
        });
    }

    async function loadGrid() {
        const res = await fetch(`/Admin/GetTimetableGrid?classType=${state.classType}`);
        if (!res.ok) {
            state.entries = [];
        } else {
            state.entries = await res.json();
        }
        renderEntries();
    }

    function renderEntries() {
        // Clear existing lesson blocks and restore plain cells first.
        document.querySelectorAll("#gridBody td.tt-cell").forEach(td => {
            td.innerHTML = "";
            td.style.display = "";
            td.removeAttribute("colspan");
        });

        const visibleEntries = state.classId
            ? state.entries.filter(e => e.classId === state.classId)
            : state.entries;

        visibleEntries.forEach(entry => {
            const startHour = parseInt(entry.startTime.split(":")[0], 10);
            const endHour = parseInt(entry.endTime.split(":")[0], 10);
            const span = Math.max(1, endHour - startHour);

            const row = document.querySelector(`#gridBody tr[data-day="${entry.dayOfWeek}"]`);
            if (!row) return;
            const startCell = row.querySelector(`td.tt-cell[data-hour="${startHour}"]`);
            if (!startCell) return;

            startCell.colSpan = span;
            const block = document.createElement("div");
            block.className = "tt-lesson-block";
            block.innerHTML = `<span class="course">${escapeHtml(entry.courseName)}</span>
                                ${escapeHtml(entry.className)}<br/>
                                ${escapeHtml(entry.teacherName)}${entry.roomName ? " @ " + escapeHtml(entry.roomName) : ""}`;
            block.addEventListener("click", () => openModalForEdit(entry));
            startCell.appendChild(block);

            // Hide the cells this lesson now spans over.
            for (let h = startHour + 1; h < startHour + span; h++) {
                const covered = row.querySelector(`td.tt-cell[data-hour="${h}"]`);
                if (covered) covered.style.display = "none";
            }
        });
    }

    function formatHour(h) {
        const period = h >= 12 ? "PM" : "AM";
        const display = h > 12 ? h - 12 : h;
        return `${display}:00 ${period}`;
    }

    function escapeHtml(str) {
        const div = document.createElement("div");
        div.textContent = str ?? "";
        return div.innerHTML;
    }

    // ------------------------------------------------------------------
    // Lesson modal (create / edit)
    // ------------------------------------------------------------------

    function bindModal() {
        document.getElementById("cancelLessonBtn").addEventListener("click", closeModal);
        document.getElementById("lessonForm").addEventListener("submit", onSubmitLesson);
        document.getElementById("deleteLessonBtn").addEventListener("click", onDeleteLesson);
    }

    function openModalForCreate(day, teacherId, teacherName, hour) {
        hideModalError();
        document.getElementById("lessonModalTitle").textContent = "Assign Lesson";
        document.getElementById("fTimetableId").value = "";
        document.getElementById("fTeacherId").value = teacherId;
        document.getElementById("fTeacherName").value = teacherName;
        document.getElementById("fDayOfWeek").value = day;
        document.getElementById("fClassType").value = state.classType;
        document.getElementById("fCourseId").value = "";
        document.getElementById("fClassId").value = state.classId || "";
        document.getElementById("fClassroomId").value = "";
        document.getElementById("fStartTime").value = `${String(hour).padStart(2, "0")}:00`;
        document.getElementById("fEndTime").value = `${String(hour + 1).padStart(2, "0")}:00`;
        document.getElementById("deleteLessonBtn").style.display = "none";
        showModal();
    }

    function openModalForEdit(entry) {
        hideModalError();
        document.getElementById("lessonModalTitle").textContent = "Edit Lesson";
        document.getElementById("fTimetableId").value = entry.timetableId;
        document.getElementById("fTeacherId").value = entry.teacherId;
        document.getElementById("fTeacherName").value = entry.teacherName;
        document.getElementById("fDayOfWeek").value = entry.dayOfWeek;
        document.getElementById("fClassType").value = entry.classType === 0 ? "School" : "Tuition";
        document.getElementById("fCourseId").value = entry.courseId;
        document.getElementById("fClassId").value = entry.classId;
        document.getElementById("fClassroomId").value = entry.classroomId || "";
        document.getElementById("fStartTime").value = entry.startTime;
        document.getElementById("fEndTime").value = entry.endTime;
        document.getElementById("deleteLessonBtn").style.display = "inline-block";
        document.getElementById("deleteLessonBtn").dataset.timetableId = entry.timetableId;
        showModal();
    }

    function showModal() { document.getElementById("lessonModal").style.display = "flex"; }
    function closeModal() { document.getElementById("lessonModal").style.display = "none"; }
    function showModalError(msg) {
        const el = document.getElementById("lessonModalError");
        el.textContent = msg;
        el.style.display = "block";
    }
    function hideModalError() {
        document.getElementById("lessonModalError").style.display = "none";
    }

    async function onSubmitLesson(e) {
        e.preventDefault();
        hideModalError();

        const timetableId = document.getElementById("fTimetableId").value;
        const payload = {
            timetableId: timetableId ? parseInt(timetableId, 10) : null,
            teacherId: parseInt(document.getElementById("fTeacherId").value, 10),
            courseId: parseInt(document.getElementById("fCourseId").value, 10),
            classId: parseInt(document.getElementById("fClassId").value, 10),
            classroomId: document.getElementById("fClassroomId").value
                ? parseInt(document.getElementById("fClassroomId").value, 10)
                : null,
            dayOfWeek: parseInt(document.getElementById("fDayOfWeek").value, 10),
            startTime: document.getElementById("fStartTime").value,
            endTime: document.getElementById("fEndTime").value,
            classType: document.getElementById("fClassType").value === "School" ? 0 : 1
        };

        if (!payload.courseId || !payload.classId) {
            showModalError("Please select both a course and a class.");
            return;
        }

        const url = timetableId ? "/Admin/UpdateTimetableEntry" : "/Admin/CreateTimetableEntry";
        const res = await fetch(url, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        if (!res.ok) {
            const text = await res.text();
            showModalError(text || "Could not save this lesson.");
            return;
        }

        closeModal();
        await loadGrid();
    }

    async function onDeleteLesson(e) {
        const id = e.target.dataset.timetableId;
        if (!id) return;
        if (!confirm("Delete this lesson?")) return;

        const res = await fetch(`/Admin/DeleteTimetableEntry?id=${id}`, { method: "POST" });
        if (!res.ok) {
            showModalError("Could not delete this lesson.");
            return;
        }
        closeModal();
        await loadGrid();
    }

    // ------------------------------------------------------------------
    // Lookup management modal (Course / Class / Classroom)
    // ------------------------------------------------------------------

    function bindLookupModal() {
        document.getElementById("closeLookupModalBtn").addEventListener("click", () => {
            document.getElementById("lookupModal").style.display = "none";
        });

        document.querySelectorAll("[data-add]").forEach(btn => {
            btn.addEventListener("click", () => onAddLookup(btn.dataset.add));
        });
    }

    function openLookupModal() {
        renderLookupList("courseLookupList", state.courses, "DeleteCourse");
        renderLookupList("classLookupList", state.classes, "DeleteClass");
        renderLookupList("classroomLookupList", state.classrooms, "DeleteClassroom");
        document.getElementById("lookupModal").style.display = "flex";
    }

    function renderLookupList(listId, items, deleteAction) {
        const list = document.getElementById(listId);
        list.innerHTML = "";
        items.forEach(i => {
            const li = document.createElement("li");
            const span = document.createElement("span");
            span.textContent = i.name;
            const btn = document.createElement("button");
            btn.textContent = "✕";
            btn.addEventListener("click", async () => {
                const res = await fetch(`/Admin/${deleteAction}?id=${i.id}`, { method: "POST" });
                if (!res.ok) {
                    alert(await res.text());
                    return;
                }
                await Promise.all([loadCourses(), loadClasses(), loadClassrooms()]);
                openLookupModal();
                await loadGrid();
            });
            li.appendChild(span);
            li.appendChild(btn);
            list.appendChild(li);
        });
    }

    async function onAddLookup(kind) {
        const inputMap = { course: "newCourseName", class: "newClassName", classroom: "newClassroomName" };
        const actionMap = { course: "CreateCourse", class: "CreateClass", classroom: "CreateClassroom" };

        const input = document.getElementById(inputMap[kind]);
        const name = input.value.trim();
        if (!name) return;

        const res = await fetch(`/Admin/${actionMap[kind]}`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ id: 0, name })
        });

        if (!res.ok) {
            alert(await res.text());
            return;
        }

        input.value = "";
        await Promise.all([loadCourses(), loadClasses(), loadClassrooms()]);
        openLookupModal();
    }
})();