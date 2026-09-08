document.addEventListener("DOMContentLoaded", () => {

    const PAGE_SIZE = 5;

    // ==================================================
    // MAIN ELEMENTS
    // ==================================================

    const searchInput =
        document.getElementById("userSearch");

    const tableBody =
        document.getElementById("userTableBody");

    const pagination =
        document.getElementById("pagination");

    const paginationSummary =
        document.getElementById("paginationSummary");

    const emptyRow =
        document.getElementById("emptyRow");


    // If this is not the User Management page,
    // stop this JS safely.
    if (
        !searchInput ||
        !tableBody ||
        !pagination ||
        !paginationSummary ||
        !emptyRow
    ) {
        return;
    }


    // ==================================================
    // FILTER ELEMENTS
    // ==================================================

    const filterTrigger =
        document.getElementById("filterTrigger");

    const filterPanel =
        document.getElementById("filterPanel");

    const filterClose =
        document.getElementById("filterClose");

    const filterApply =
        document.getElementById("filterApply");

    const filterReset =
        document.getElementById("filterReset");

    const filterCount =
        document.getElementById("filterCount");

    const refreshUsers =
        document.getElementById("refreshUsers");


    // ==================================================
    // USER DETAIL ELEMENTS
    // ==================================================

    const detailOverlay =
        document.getElementById("detailOverlay");

    const detailClose =
        document.getElementById("detailClose");


    // ==================================================
    // USER ROWS
    // ==================================================

    const allRows =
        Array.from(
            tableBody.querySelectorAll(".user-row")
        );


    allRows.forEach((row, index) => {

        const originalNo =
            index + 1;

        row.dataset.originalIndex =
            index.toString();

        row.dataset.originalNo =
            originalNo.toString();

        const rowNumber =
            row.querySelector(".row-number");

        if (rowNumber) {
            rowNumber.textContent =
                originalNo.toString();
        }
    });


    // ==================================================
    // PAGINATION / FILTER STATE
    // ==================================================

    let currentPage = 1;

    let activeRole = "all";
    let activeStatus = "all";

    let activeSortBy = "default";
    let activeSortOrder = "asc";


    // ==================================================
    // FILTER HELPERS
    // ==================================================

    function getCheckedValue(name) {

        const checked =
            document.querySelector(
                `input[name="${name}"]:checked`
            );

        return checked
            ? checked.value
            : "";
    }


    function setCheckedValue(
        name,
        value
    ) {

        const input =
            document.querySelector(
                `input[name="${name}"][value="${value}"]`
            );

        if (input) {
            input.checked = true;
        }
    }


    // ==================================================
    // SEARCH + FILTER + SORT
    // ==================================================

    function getProcessedRows() {

        const searchText =
            searchInput
                .value
                .trim()
                .toLowerCase();


        // SEARCH + FILTER
        let rows =
            allRows.filter(row => {

                const username =
                    (
                        row.dataset.username ||
                        ""
                    ).toLowerCase();

                const role =
                    row.dataset.position ||
                    "";

                const status =
                    row.dataset.status ||
                    "";


                const matchesSearch =
                    username.includes(
                        searchText
                    );


                const matchesRole =
                    activeRole === "all" ||
                    role === activeRole;


                const matchesStatus =
                    activeStatus === "all"
                        ? status !== "Deleted"
                        : status === activeStatus;


                return (
                    matchesSearch &&
                    matchesRole &&
                    matchesStatus
                );
            });


        // SORT
        rows.sort((a, b) => {

            let comparison = 0;


            if (
                activeSortBy ===
                "id"
            ) {

                comparison =
                    (
                        a.dataset.userId ||
                        ""
                    ).localeCompare(
                        b.dataset.userId ||
                        "",
                        undefined,
                        {
                            numeric: true,
                            sensitivity: "base"
                        }
                    );
            }

            else if (
                activeSortBy ===
                "username"
            ) {

                comparison =
                    (
                        a.dataset.username ||
                        ""
                    ).localeCompare(
                        b.dataset.username ||
                        "",
                        undefined,
                        {
                            numeric: true,
                            sensitivity: "base"
                        }
                    );
            }

            else {

                comparison =
                    Number(
                        a.dataset.originalIndex
                    ) -
                    Number(
                        b.dataset.originalIndex
                    );
            }


            return (
                activeSortOrder ===
                "desc"
            )
                ? -comparison
                : comparison;
        });


        return rows;
    }


    // ==================================================
    // RENDER USERS
    // ==================================================

    function renderUsers() {

        const processedRows =
            getProcessedRows();

        const totalItems =
            processedRows.length;


        // Hide every row first
        allRows.forEach(row => {

            row.hidden = true;
        });


        // Reorder rows
        processedRows.forEach(row => {

            tableBody.appendChild(
                row
            );
        });


        // Empty row always last
        tableBody.appendChild(
            emptyRow
        );


        // Keep original NO
        processedRows.forEach(row => {

            const rowNumber =
                row.querySelector(
                    ".row-number"
                );

            if (rowNumber) {

                rowNumber.textContent =
                    row.dataset.originalNo ||
                    "";
            }
        });


        // No results
        if (
            totalItems === 0
        ) {

            currentPage = 1;

            emptyRow.hidden =
                false;

            paginationSummary.textContent =
                "No matching users";

            pagination.innerHTML =
                "";

            return;
        }


        emptyRow.hidden =
            true;


        // Calculate pages
        const totalPages =
            Math.ceil(
                totalItems /
                PAGE_SIZE
            );


        if (
            currentPage >
            totalPages
        ) {
            currentPage =
                totalPages;
        }


        if (
            currentPage < 1
        ) {
            currentPage = 1;
        }


        const startIndex =
            (currentPage - 1) *
            PAGE_SIZE;


        const endIndex =
            Math.min(
                startIndex +
                PAGE_SIZE,
                totalItems
            );


        const rowsForCurrentPage =
            processedRows.slice(
                startIndex,
                endIndex
            );


        rowsForCurrentPage.forEach(
            row => {

                row.hidden =
                    false;
            }
        );


        paginationSummary.textContent =
            `Showing ${startIndex + 1}–${endIndex} of ${totalItems}`;


        renderPagination(
            totalPages
        );
    }


    // ==================================================
    // PAGINATION
    // ==================================================

    function renderPagination(
        totalPages
    ) {

        pagination.innerHTML =
            "";


        if (
            totalPages <= 1
        ) {
            return;
        }


        // Previous
        pagination.appendChild(
            createPaginationButton(
                "‹",
                currentPage - 1,
                currentPage === 1,
                false,
                "Previous page"
            )
        );


        // Page numbers
        for (
            let page = 1;
            page <= totalPages;
            page++
        ) {

            pagination.appendChild(
                createPaginationButton(
                    page.toString(),
                    page,
                    false,
                    page === currentPage,
                    `Page ${page}`
                )
            );
        }


        // Next
        pagination.appendChild(
            createPaginationButton(
                "›",
                currentPage + 1,
                currentPage === totalPages,
                false,
                "Next page"
            )
        );
    }


    function createPaginationButton(
        text,
        page,
        disabled,
        active,
        label
    ) {

        const button =
            document.createElement(
                "button"
            );


        button.type =
            "button";

        button.className =
            "page-button";

        button.textContent =
            text;

        button.disabled =
            disabled;


        button.setAttribute(
            "aria-label",
            label
        );


        if (active) {

            button.classList.add(
                "active"
            );

            button.setAttribute(
                "aria-current",
                "page"
            );
        }


        button.addEventListener(
            "click",
            () => {

                if (
                    disabled ||
                    page === currentPage
                ) {
                    return;
                }

                currentPage =
                    page;

                renderUsers();
            }
        );


        return button;
    }


    // ==================================================
    // FILTER OPEN / CLOSE
    // ==================================================

    function openFilter() {

        if (!filterPanel) {
            return;
        }

        filterPanel.hidden =
            false;


        if (filterTrigger) {

            filterTrigger.setAttribute(
                "aria-expanded",
                "true"
            );
        }
    }


    function closeFilter() {

        if (!filterPanel) {
            return;
        }

        filterPanel.hidden =
            true;


        if (filterTrigger) {

            filterTrigger.setAttribute(
                "aria-expanded",
                "false"
            );
        }
    }


    // ==================================================
    // REGISTER STUDENT PROGRAM ELEMENTS
    // ==================================================

    const registerOverlay =
        document.getElementById(
            "registerOverlay"
        );

    const registerClose =
        document.getElementById(
            "registerClose"
        );

    const registerCancel =
        document.getElementById(
            "registerCancel"
        );

    const registerUserId =
        document.getElementById(
            "registerUserId"
        );

    const registerUsername =
        document.getElementById(
            "registerUsername"
        );

    const registerProgramSelect =
        document.getElementById(
            "registerProgramSelect"
        );

    const registerClassSelect =
        document.getElementById(
            "registerClassSelect"
        );


    // ==================================================
    // MANAGE TEACHER SUBJECT ELEMENTS
    // ==================================================

    const teacherSubjectOverlay =
        document.getElementById(
            "teacherSubjectOverlay"
        );

    const teacherSubjectClose =
        document.getElementById(
            "teacherSubjectClose"
        );

    const teacherSubjectCancel =
        document.getElementById(
            "teacherSubjectCancel"
        );

    const teacherSubjectTeacherId =
        document.getElementById(
            "teacherSubjectTeacherId"
        );

    const teacherSubjectTeacherName =
        document.getElementById(
            "teacherSubjectTeacherName"
        );

    const teacherProgramList =
        document.getElementById(
            "teacherProgramList"
        );

    const teacherSubjectList =
        document.getElementById(
            "teacherSubjectList"
        );

    const teacherSubjectNoProgram =
        document.getElementById(
            "teacherSubjectNoProgram"
        );

    const teacherSubjectActions =
        document.getElementById(
            "teacherSubjectActions"
        );


    // ==================================================
    // PROGRAM / CLASS DATA
    // ==================================================

    let programsData = [];
    let classesData = [];


    const programsJson =
        document.getElementById(
            "programsJson"
        );


    const classesJson =
        document.getElementById(
            "classesJson"
        );


    try {

        programsData =
            JSON.parse(
                programsJson?.textContent ||
                "[]"
            );
    }

    catch {

        programsData =
            [];
    }


    try {

        classesData =
            JSON.parse(
                classesJson?.textContent ||
                "[]"
            );
    }

    catch {

        classesData =
            [];
    }


    // ==================================================
    // POPULATE PROGRAMS
    // ==================================================

    function populatePrograms(
        selectedProgramId
    ) {

        if (
            !registerProgramSelect
        ) {
            return;
        }


        registerProgramSelect.innerHTML =
            "";


        programsData.forEach(p => {

            const option =
                document.createElement(
                    "option"
                );


            option.value =
                p.ProgramId;


            option.textContent =
                p.ProgramName;


            if (
                p.ProgramId ===
                selectedProgramId
            ) {

                option.selected =
                    true;
            }


            registerProgramSelect
                .appendChild(
                    option
                );
        });
    }


    // ==================================================
    // POPULATE CLASSES
    // ==================================================

    function populateClasses(
        programId,
        selectedClassId
    ) {

        if (
            !registerClassSelect
        ) {
            return;
        }


        registerClassSelect.innerHTML =
            "";


        const filtered =
            classesData.filter(
                c =>
                    c.ProgramId ===
                    programId
            );


        filtered.forEach(c => {

            const option =
                document.createElement(
                    "option"
                );


            option.value =
                c.ClassId;


            option.textContent =
                c.ClassName;


            if (
                c.ClassId ===
                selectedClassId
            ) {

                option.selected =
                    true;
            }


            registerClassSelect
                .appendChild(
                    option
                );
        });
    }


    // ==================================================
    // OPEN REGISTER PROGRAM
    // ==================================================

    function openRegisterProgram(
        row
    ) {

        if (
            !registerOverlay ||
            !registerUserId ||
            !registerUsername ||
            !registerProgramSelect ||
            !registerClassSelect
        ) {

            alert(
                "Register Program panel was not found."
            );

            return;
        }


        const userId =
            row.dataset.userId;


        const username =
            row.dataset.username;


        const currentProgramId =
            row.dataset.programId ||
            "";


        const currentClassId =
            row.dataset.classId ||
            "";


        registerUserId.value =
            userId;


        registerUsername.textContent =
            username;


        const initialProgramId =
            currentProgramId ||
            (
                programsData[0]
                    ? programsData[0]
                        .ProgramId
                    : ""
            );


        populatePrograms(
            initialProgramId
        );


        populateClasses(
            initialProgramId,
            currentClassId
        );


        registerOverlay.hidden =
            false;


        document.body.classList.add(
            "drawer-open"
        );
    }


    // ==================================================
    // CLOSE REGISTER PROGRAM
    // ==================================================

    function closeRegisterProgram() {

        if (
            registerOverlay
        ) {

            registerOverlay.hidden =
                true;
        }


        document.body.classList.remove(
            "drawer-open"
        );
    }


    // ==================================================
    // REGISTER PROGRAM EVENTS
    // ==================================================

    if (
        registerProgramSelect
    ) {

        registerProgramSelect
            .addEventListener(
                "change",
                () => {

                    populateClasses(
                        registerProgramSelect.value,
                        ""
                    );
                }
            );
    }


    if (
        registerClose
    ) {

        registerClose
            .addEventListener(
                "click",
                closeRegisterProgram
            );
    }


    if (
        registerCancel
    ) {

        registerCancel
            .addEventListener(
                "click",
                closeRegisterProgram
            );
    }


    if (
        registerOverlay
    ) {

        registerOverlay
            .addEventListener(
                "click",
                event => {

                    if (
                        event.target ===
                        registerOverlay
                    ) {

                        closeRegisterProgram();
                    }
                }
            );
    }


    // ==================================================
    // OPEN MANAGE TEACHER SUBJECTS
    // ==================================================

    async function openTeacherSubjects(
        row
    ) {

        if (
            !teacherSubjectOverlay ||
            !teacherSubjectTeacherId ||
            !teacherSubjectTeacherName ||
            !teacherProgramList ||
            !teacherSubjectList ||
            !teacherSubjectNoProgram ||
            !teacherSubjectActions
        ) {
            alert("Manage Teacher Programs & Subjects panel was not found.");
            return;
        }

        const teacherId = row.dataset.userId;
        const username = row.dataset.username;

        teacherSubjectTeacherId.value = teacherId;
        teacherSubjectTeacherName.textContent = username;
        teacherProgramList.innerHTML = "Loading programs...";
        teacherSubjectList.innerHTML = "";
        teacherSubjectNoProgram.hidden = true;
        teacherSubjectActions.hidden = false;
        teacherSubjectOverlay.hidden = false;
        document.body.classList.add("drawer-open");

        try {
            const response = await fetch(
                `/Admin/GetTeacherSubjectRegistration?teacherId=${encodeURIComponent(teacherId)}`
            );

            if (!response.ok) {
                const message = await response.text();
                throw new Error(message || "Unable to load teacher registration.");
            }

            const data = await response.json();
            teacherSubjectTeacherName.textContent = data.teacherName || username;

            const programs = Array.isArray(data.programs) ? data.programs : [];
            const subjects = Array.isArray(data.subjects) ? data.subjects : [];
            const selectedCourseState = new Set(
                subjects
                    .filter(subject => Boolean(subject.selected))
                    .map(subject => subject.courseId)
            );

            teacherProgramList.innerHTML = "";

            if (programs.length === 0) {
                teacherProgramList.innerHTML = "";
                const message = document.createElement("div");
                message.className = "user-flash error";
                message.textContent = "No programs are available. Create a program first.";
                teacherProgramList.appendChild(message);
                teacherSubjectActions.hidden = true;
                return;
            }

            programs.forEach(program => {
                const label = document.createElement("label");
                label.className = "teacher-subject-item";

                const checkbox = document.createElement("input");
                checkbox.type = "checkbox";
                checkbox.name = "selectedProgramIds";
                checkbox.value = program.programId;
                checkbox.checked = Boolean(program.selected);
                checkbox.dataset.teacherProgram = "true";

                const info = document.createElement("div");
                const title = document.createElement("strong");
                title.textContent = program.programCode
                    ? `${program.programCode} · ${program.programName}`
                    : program.programName;

                const detail = document.createElement("span");
                detail.textContent = "Program";

                info.appendChild(title);
                info.appendChild(detail);
                label.appendChild(checkbox);
                label.appendChild(info);
                teacherProgramList.appendChild(label);
            });

            function getSelectedProgramIds() {
                return Array.from(
                    teacherProgramList.querySelectorAll(
                        'input[name="selectedProgramIds"]:checked'
                    )
                ).map(input => input.value);
            }

            function renderSubjects() {
                const selectedProgramIds = getSelectedProgramIds();
                teacherSubjectList.innerHTML = "";

                if (selectedProgramIds.length === 0) {
                    teacherSubjectNoProgram.hidden = false;
                    return;
                }

                teacherSubjectNoProgram.hidden = true;

                const visibleSubjects = subjects.filter(subject =>
                    Array.isArray(subject.programIds) &&
                    subject.programIds.some(programId =>
                        selectedProgramIds.includes(programId)
                    )
                );

                if (visibleSubjects.length === 0) {
                    const message = document.createElement("div");
                    message.className = "user-flash error";
                    message.textContent =
                        "No subjects are assigned to the selected program(s).";
                    teacherSubjectList.appendChild(message);
                    return;
                }

                visibleSubjects.forEach(subject => {
                    const label = document.createElement("label");
                    label.className = "teacher-subject-item";

                    const checkbox = document.createElement("input");
                    checkbox.type = "checkbox";
                    checkbox.name = "selectedCourseIds";
                    checkbox.value = subject.courseId;
                    checkbox.checked = selectedCourseState.has(subject.courseId);
                    checkbox.addEventListener("change", () => {
                        if (checkbox.checked) {
                            selectedCourseState.add(subject.courseId);
                        }
                        else {
                            selectedCourseState.delete(subject.courseId);
                        }
                    });

                    const info = document.createElement("div");
                    const title = document.createElement("strong");
                    title.textContent = subject.courseCode
                        ? `${subject.courseCode} · ${subject.courseName}`
                        : subject.courseName;

                    const program = document.createElement("span");
                    const visibleProgramNames = programs
                        .filter(p =>
                            selectedProgramIds.includes(p.programId) &&
                            subject.programIds.includes(p.programId)
                        )
                        .map(p => p.programName);

                    program.textContent = visibleProgramNames.join(", ");

                    info.appendChild(title);
                    info.appendChild(program);
                    label.appendChild(checkbox);
                    label.appendChild(info);
                    teacherSubjectList.appendChild(label);
                });
            }

            teacherProgramList
                .querySelectorAll('input[name="selectedProgramIds"]')
                .forEach(input => {
                    input.addEventListener("change", renderSubjects);
                });

            renderSubjects();
        }
        catch (error) {
            console.error(error);
            teacherProgramList.innerHTML = "";
            teacherSubjectList.innerHTML = "";

            const errorBox = document.createElement("div");
            errorBox.className = "user-flash error";
            errorBox.textContent =
                error.message || "Unable to load teacher registration.";

            teacherProgramList.appendChild(errorBox);
        }
    }

    // ==================================================
    // CLOSE MANAGE TEACHER SUBJECTS
    // ==================================================

    function closeTeacherSubjects() {

        if (
            teacherSubjectOverlay
        ) {

            teacherSubjectOverlay.hidden =
                true;
        }


        document.body.classList.remove(
            "drawer-open"
        );
    }


    // ==================================================
    // MANAGE SUBJECT EVENTS
    // ==================================================

    /*
     * IMPORTANT:
     * Check if element exists before
     * calling addEventListener.
     *
     * Otherwise JS can crash here and
     * pagination will disappear.
     */

    if (
        teacherSubjectClose
    ) {

        teacherSubjectClose
            .addEventListener(
                "click",
                closeTeacherSubjects
            );
    }


    if (
        teacherSubjectCancel
    ) {

        teacherSubjectCancel
            .addEventListener(
                "click",
                closeTeacherSubjects
            );
    }


    if (
        teacherSubjectOverlay
    ) {

        teacherSubjectOverlay
            .addEventListener(
                "click",
                event => {

                    if (
                        event.target ===
                        teacherSubjectOverlay
                    ) {

                        closeTeacherSubjects();
                    }
                }
            );
    }


    // ==================================================
    // FILTER COUNT
    // ==================================================

    function updateFilterCount() {

        if (
            !filterCount
        ) {
            return;
        }


        let count = 0;


        if (
            activeRole !==
            "all"
        ) {

            count++;
        }


        if (
            activeStatus !==
            "all"
        ) {

            count++;
        }


        if (
            activeSortBy !==
            "default" ||
            activeSortOrder !==
            "asc"
        ) {

            count++;
        }


        filterCount.textContent =
            count.toString();


        filterCount.hidden =
            count === 0;
    }


    // ==================================================
    // APPLY FILTER
    // ==================================================

    function applyFilters() {

        activeRole =
            getCheckedValue(
                "roleFilter"
            ) ||
            "all";


        activeStatus =
            getCheckedValue(
                "statusFilter"
            ) ||
            "all";


        activeSortBy =
            getCheckedValue(
                "sortByFilter"
            ) ||
            "default";


        activeSortOrder =
            getCheckedValue(
                "sortOrderFilter"
            ) ||
            "asc";


        currentPage =
            1;


        updateFilterCount();

        renderUsers();

        closeFilter();
    }


    // ==================================================
    // RESET FILTER
    // ==================================================

    function resetFilters() {

        activeRole =
            "all";

        activeStatus =
            "all";

        activeSortBy =
            "default";

        activeSortOrder =
            "asc";


        setCheckedValue(
            "roleFilter",
            "all"
        );


        setCheckedValue(
            "statusFilter",
            "all"
        );


        setCheckedValue(
            "sortByFilter",
            "default"
        );


        setCheckedValue(
            "sortOrderFilter",
            "asc"
        );


        currentPage =
            1;


        updateFilterCount();

        renderUsers();
    }


    // ==================================================
    // OPEN USER DETAIL
    // ==================================================

    function openUserDetail(
        row
    ) {

        if (
            !detailOverlay
        ) {
            return;
        }


        const username =
            row.dataset.username ||
            "-";


        setText(
            "detailUserId",
            row.dataset.userId ||
            "-"
        );


        setText(
            "detailUsername",
            username
        );


        setText(
            "detailUsernameValue",
            username
        );


        setText(
            "detailEmail",
            row.dataset.email ||
            "-"
        );


        setText(
            "detailPhone",
            row.dataset.phone ||
            "-"
        );


        setText(
            "detailAddress",
            row.dataset.address ||
            "-"
        );


        setText(
            "detailGender",
            row.dataset.gender ||
            "-"
        );


        setText(
            "detailPosition",
            row.dataset.position ||
            "-"
        );


        setText(
            "detailRole",
            row.dataset.position ||
            "-"
        );


        setText(
            "detailStatus",
            row.dataset.status ||
            "-"
        );


        const deletedAtItem =
            document.getElementById(
                "detailDeletedAtItem"
            );


        const isDeleted =
            row.dataset.isDeleted ===
            "true";


        if (
            deletedAtItem
        ) {

            deletedAtItem.hidden =
                !isDeleted;
        }


        setText(
            "detailDeletedAt",
            row.dataset.deletedAt ||
            "-"
        );


        showAvatar(
            row.dataset.avatar
        );


        detailOverlay.hidden =
            false;


        document.body.classList.add(
            "drawer-open"
        );
    }


    // ==================================================
    // CLOSE USER DETAIL
    // ==================================================

    function closeUserDetail() {

        if (
            detailOverlay
        ) {

            detailOverlay.hidden =
                true;
        }


        document.body.classList.remove(
            "drawer-open"
        );
    }


    // ==================================================
    // SET TEXT
    // ==================================================

    function setText(
        id,
        value
    ) {

        const element =
            document.getElementById(
                id
            );


        if (
            element
        ) {

            element.textContent =
                value;
        }
    }


    // ==================================================
    // SHOW AVATAR
    // ==================================================

    function showAvatar(
        avatarValue
    ) {

        const image =
            document.getElementById(
                "detailAvatar"
            );


        const fallback =
            document.getElementById(
                "detailAvatarFallback"
            );


        if (
            !image ||
            !fallback
        ) {
            return;
        }


        const username =
            document.getElementById(
                "detailUsername"
            )?.textContent ||
            "?";


        image.hidden =
            true;


        image.removeAttribute(
            "src"
        );


        fallback.hidden =
            false;


        fallback.textContent =
            username
                .trim()
                .charAt(0)
                .toUpperCase() ||
            "👤";


        if (
            !avatarValue ||
            avatarValue === "-" ||
            avatarValue
                .toLowerCase() ===
            "null"
        ) {

            return;
        }


        const candidates =
            [];


        if (
            avatarValue.startsWith("/") ||
            avatarValue.startsWith(
                "http://"
            ) ||
            avatarValue.startsWith(
                "https://"
            )
        ) {

            candidates.push(
                avatarValue
            );
        }

        else {

            candidates.push(
                `/uploads/avatars/${avatarValue}`,
                `/images/avatars/${avatarValue}`,
                `/avatars/${avatarValue}`,
                `/images/${avatarValue}`
            );
        }


        let candidateIndex =
            0;


        const tryNext =
            () => {

                if (
                    candidateIndex >=
                    candidates.length
                ) {

                    image.hidden =
                        true;


                    fallback.hidden =
                        false;


                    return;
                }


                image.src =
                    candidates[
                    candidateIndex
                    ];


                candidateIndex++;
            };


        image.onload =
            () => {

                image.hidden =
                    false;


                fallback.hidden =
                    true;
            };


        image.onerror =
            tryNext;


        tryNext();
    }


    // ==================================================
    // PROMOTE TO TEACHER
    // ==================================================

    async function promoteTeacher(
        row
    ) {

        const id =
            row.dataset.userId;


        const username =
            row.dataset.username;


        const confirmed =
            confirm(
                `Promote ${username} to Teacher?`
            );


        if (
            !confirmed
        ) {
            return;
        }


        try {

            const response =
                await fetch(
                    `/Admin/PromoteToTeacher?id=${encodeURIComponent(id)}`,
                    {
                        method: "POST"
                    }
                );


            if (
                !response.ok
            ) {

                const message =
                    await response.text();


                alert(
                    message
                );


                return;
            }


            alert(
                `${username} has been promoted to Teacher.`
            );


            window.location.reload();
        }

        catch (error) {

            console.error(
                error
            );


            alert(
                "Unable to promote user to Teacher."
            );
        }
    }


    // ==================================================
    // REMOVE TEACHER ROLE
    // ==================================================

    async function cancelTeacher(
        row
    ) {

        const id =
            row.dataset.userId;


        const username =
            row.dataset.username;


        const confirmed =
            confirm(
                `Remove Teacher role from ${username}?`
            );


        if (
            !confirmed
        ) {
            return;
        }


        try {

            const response =
                await fetch(
                    `/Admin/CancelTeacherRole?id=${encodeURIComponent(id)}`,
                    {
                        method: "POST"
                    }
                );


            if (
                !response.ok
            ) {

                const message =
                    await response.text();


                alert(
                    message
                );


                return;
            }


            alert(
                `${username} is now a Student.`
            );


            window.location.reload();
        }

        catch (error) {

            console.error(
                error
            );


            alert(
                "Unable to remove Teacher role."
            );
        }
    }


    // ==================================================
    // DELETE ACCOUNT CONFIRMATION
    // ==================================================

    document
        .querySelectorAll(
            ".delete-account-form"
        )
        .forEach(
            form => {

                form.addEventListener(
                    "submit",
                    event => {

                        const username =
                            form.dataset.username ||
                            "this user";


                        const userId =
                            form.dataset.userId ||
                            "";


                        const confirmed =
                            confirm(
                                `Delete ${username} (${userId})?\n\n` +
                                "The account will no longer be able to sign in. Username, email and phone may be reused by a new account.\n\n" +
                                "Attendance, payment, chat and other historical records will NOT be deleted. The old User ID will remain permanently reserved."
                            );


                        if (
                            !confirmed
                        ) {

                            event.preventDefault();
                        }
                    }
                );
            }
        );


    // ==================================================
    // REFRESH
    // ==================================================

    if (
        refreshUsers
    ) {

        refreshUsers
            .addEventListener(
                "click",
                () => {

                    window.location.reload();
                }
            );
    }


    // ==================================================
    // SEARCH
    // ==================================================

    searchInput.addEventListener(
        "input",
        () => {

            currentPage =
                1;


            renderUsers();
        }
    );


    // ==================================================
    // FILTER EVENTS
    // ==================================================

    if (
        filterTrigger
    ) {

        filterTrigger
            .addEventListener(
                "click",
                event => {

                    event.stopPropagation();


                    if (
                        filterPanel &&
                        filterPanel.hidden
                    ) {

                        openFilter();
                    }

                    else {

                        closeFilter();
                    }
                }
            );
    }


    if (
        filterClose
    ) {

        filterClose
            .addEventListener(
                "click",
                closeFilter
            );
    }


    if (
        filterApply
    ) {

        filterApply
            .addEventListener(
                "click",
                applyFilters
            );
    }


    if (
        filterReset
    ) {

        filterReset
            .addEventListener(
                "click",
                resetFilters
            );
    }


    document.addEventListener(
        "click",
        event => {

            if (
                filterPanel &&
                filterTrigger &&
                !filterPanel.hidden &&
                !filterPanel.contains(
                    event.target
                ) &&
                !filterTrigger.contains(
                    event.target
                )
            ) {

                closeFilter();
            }
        }
    );


    // ==================================================
    // TABLE ACTION BUTTONS
    // ==================================================

    tableBody.addEventListener(
        "click",
        event => {

            const actionButton =
                event.target.closest(
                    "[data-action]"
                );


            if (
                !actionButton
            ) {
                return;
            }


            const row =
                actionButton.closest(
                    ".user-row"
                );


            if (
                !row
            ) {
                return;
            }


            const action =
                actionButton
                    .dataset
                    .action;


            // Detail
            if (
                action ===
                "detail"
            ) {

                openUserDetail(
                    row
                );
            }


            // Make Teacher
            else if (
                action ===
                "promote"
            ) {

                promoteTeacher(
                    row
                );
            }


            // Remove Teacher
            else if (
                action ===
                "cancel-teacher"
            ) {

                cancelTeacher(
                    row
                );
            }


            // Register / Edit Program
            else if (
                action ===
                "register-program"
            ) {

                openRegisterProgram(
                    row
                );
            }


            // Manage Teacher Subjects
            else if (
                action ===
                "teacher-subject"
            ) {

                openTeacherSubjects(
                    row
                );
            }
        }
    );


    // ==================================================
    // DETAIL CLOSE
    // ==================================================

    if (
        detailClose
    ) {

        detailClose
            .addEventListener(
                "click",
                closeUserDetail
            );
    }


    if (
        detailOverlay
    ) {

        detailOverlay
            .addEventListener(
                "click",
                event => {

                    if (
                        event.target ===
                        detailOverlay
                    ) {

                        closeUserDetail();
                    }
                }
            );
    }


    // ==================================================
    // ESC KEY
    // ==================================================

    document.addEventListener(
        "keydown",
        event => {

            if (
                event.key !==
                "Escape"
            ) {

                return;
            }


            // User Detail
            if (
                detailOverlay &&
                !detailOverlay.hidden
            ) {

                closeUserDetail();
            }


            // Filter
            if (
                filterPanel &&
                !filterPanel.hidden
            ) {

                closeFilter();
            }


            // Register Program
            if (
                registerOverlay &&
                !registerOverlay.hidden
            ) {

                closeRegisterProgram();
            }


            // Manage Subjects
            if (
                teacherSubjectOverlay &&
                !teacherSubjectOverlay.hidden
            ) {

                closeTeacherSubjects();
            }
        }
    );


    // ==================================================
    // INITIAL LOAD
    // ==================================================

    updateFilterCount();

    renderUsers();
});