
const DAYS = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"];
const START_HOUR = 8;
const END_HOUR = 20;


let timetableData = {}; // key: "day-hour"  value: { teacher, subject }
let currentKey = null;

const headerRow = document.getElementById("headerRow");
const tableBody = document.getElementById("tableBody");
const modalOverlay = document.getElementById("modalOverlay");
const teacherInput = document.getElementById("teacherInput");
const subjectInput = document.getElementById("subjectInput");
const modalTitle = document.getElementById("modalTitle");

function formatHour(h) {
    return String(h).padStart(2, "0") + ":00";
}

function buildHeader() {
    DAYS.forEach(day => {
        const th = document.createElement("th");
        th.textContent = day;
        headerRow.appendChild(th);
    });
}

function buildBody() {
    for (let hour = START_HOUR; hour < END_HOUR; hour++) {
        const tr = document.createElement("tr");

        const timeTd = document.createElement("td");
        timeTd.className = "time-cell";
        timeTd.textContent = `${formatHour(hour)} - ${formatHour(hour + 1)}`;
        tr.appendChild(timeTd);

        DAYS.forEach(day => {
            const key = `${day}-${hour}`;
            const td = document.createElement("td");
            td.className = "slot";
            td.dataset.key = key;
            td.addEventListener("click", () => openModal(key, day, hour));
            tr.appendChild(td);
        });

        tableBody.appendChild(tr);
    }
}

function renderCell(key) {
    const td = document.querySelector(`.slot[data-key="${key}"]`);
    if (!td) return;

    const entry = timetableData[key];
    if (entry && (entry.teacher || entry.subject)) {
        td.classList.add("filled");
        td.innerHTML = `
      <div class="subject">${escapeHtml(entry.subject || "")}</div>
      <div class="teacher">${escapeHtml(entry.teacher || "")}</div>
    `;
    } else {
        td.classList.remove("filled");
        td.innerHTML = "";
    }
}

function renderAll() {
    Object.keys(timetableData).forEach(renderCell);
}

function escapeHtml(str) {
    const div = document.createElement("div");
    div.textContent = str;
    return div.innerHTML;
}

function openModal(key, day, hour) {
    currentKey = key;
    const entry = timetableData[key] || { teacher: "", subject: "" };
    teacherInput.value = entry.teacher || "";
    subjectInput.value = entry.subject || "";
    modalTitle.textContent = `${day}  ${formatHour(hour)} - ${formatHour(hour + 1)}`;
    modalOverlay.classList.remove("hidden");
    subjectInput.focus();
}

function closeModal() {
    modalOverlay.classList.add("hidden");
    currentKey = null;
}

async function saveTimetable() {
    await fetch("/Admin/SaveTimetableData", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(timetableData)
    });
}

document.getElementById("saveBtn").addEventListener("click", async () => {
    if (!currentKey) return;
    const teacher = teacherInput.value.trim();
    const subject = subjectInput.value.trim();

    if (!teacher && !subject) {
        delete timetableData[currentKey];
    } else {
        timetableData[currentKey] = { teacher, subject };
    }

    renderCell(currentKey);
    await saveTimetable();
    closeModal();
});

document.getElementById("clearBtn").addEventListener("click", async () => {
    if (!currentKey) return;
    delete timetableData[currentKey];
    renderCell(currentKey);
    await saveTimetable();
    closeModal();
});

document.getElementById("cancelBtn").addEventListener("click", closeModal);
modalOverlay.addEventListener("click", (e) => {
    if (e.target === modalOverlay) closeModal();
});

async function loadTimetable() {
    try {
        const res = await fetch("/Admin/GetTimetableData");
        timetableData = await res.json();
    } catch (err) {
        console.error("加载课表失败", err);
        timetableData = {};
    }
    renderAll();
}

buildHeader();
buildBody();
loadTimetable();
