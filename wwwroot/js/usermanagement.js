document.addEventListener("DOMContentLoaded", () => {
    const PAGE_SIZE = 5;

    const searchInput = document.getElementById("userSearch");
    const tableBody = document.getElementById("userTableBody");
    const pagination = document.getElementById("pagination");
    const paginationSummary = document.getElementById("paginationSummary");
    const emptyRow = document.getElementById("emptyRow");

    const filterTrigger = document.getElementById("filterTrigger");
    const filterPanel = document.getElementById("filterPanel");
    const filterClose = document.getElementById("filterClose");
    const filterApply = document.getElementById("filterApply");
    const filterReset = document.getElementById("filterReset");
    const filterCount = document.getElementById("filterCount");
    const refreshUsers = document.getElementById("refreshUsers");

    const detailOverlay = document.getElementById("detailOverlay");
    const detailClose = document.getElementById("detailClose");

    const allRows = Array.from(
        tableBody.querySelectorAll(".user-row")
    );

    /*
     * originalIndex is the stable default display order received from the server.
     * originalNo is the fixed display number assigned once when the page loads.
     * NO never changes after Search, Filter, Sort or Pagination.
     * User ID is the real database key and is shown separately.
     */
    allRows.forEach((row, index) => {
        const originalNo = index + 1;

        row.dataset.originalIndex = index.toString();
        row.dataset.originalNo = originalNo.toString();

        const rowNumber =
            row.querySelector(".row-number");

        if (rowNumber) {
            rowNumber.textContent =
                originalNo.toString();
        }
    });

    let currentPage = 1;

    let activeRole = "all";
    let activeStatus = "all";

    let activeSortBy = "default";
    let activeSortOrder = "asc";

    function getCheckedValue(name) {
        const checked = document.querySelector(
            `input[name="${name}"]:checked`
        );

        return checked ? checked.value : "";
    }

    function setCheckedValue(name, value) {
        const input = document.querySelector(
            `input[name="${name}"][value="${value}"]`
        );

        if (input) {
            input.checked = true;
        }
    }

    /*
     * Processing order:
     *
     * Search
     *   ↓
     * Filter
     *   ↓
     * Sort ALL matching results
     *   ↓
     * Reorder the table rows
     *   ↓
     * Keep original NO
     *   ↓
     * Pagination LAST
     */
    function getProcessedRows() {
        const searchText = searchInput.value
            .trim()
            .toLowerCase();

        // 1. SEARCH + FILTER ALL USERS FIRST
        let rows = allRows.filter(row => {
            const username = (
                row.dataset.username || ""
            ).toLowerCase();

            const role =
                row.dataset.position || "";

            const status =
                row.dataset.status || "";

            const matchesSearch =
                username.includes(searchText);

            const matchesRole =
                activeRole === "all" ||
                role === activeRole;

            const matchesStatus =
                activeStatus === "all" ||
                status === activeStatus;

            return matchesSearch &&
                matchesRole &&
                matchesStatus;
        });

        // 2. SORT THE COMPLETE FILTERED RESULT
        rows.sort((a, b) => {
            let comparison = 0;

            if (activeSortBy === "id") {
                comparison =
                    (a.dataset.userId || "").localeCompare(
                        b.dataset.userId || "",
                        undefined,
                        { numeric: true, sensitivity: "base" }
                    );
            }
            else if (activeSortBy === "username") {
                comparison = (
                    a.dataset.username || ""
                ).localeCompare(
                    b.dataset.username || "",
                    undefined,
                    {
                        sensitivity: "base",
                        numeric: true
                    }
                );
            }
            else {
                // Default (NO): keep the original list order from the server.
                comparison =
                    Number(a.dataset.originalIndex) -
                    Number(b.dataset.originalIndex);
            }

            return activeSortOrder === "desc"
                ? -comparison
                : comparison;
        });

        return rows;
    }

    function renderUsers() {
        const processedRows =
            getProcessedRows();

        const totalItems =
            processedRows.length;

        // Hide every real row first.
        allRows.forEach(row => {
            row.hidden = true;
        });

        /*
         * IMPORTANT:
         * Physically reorder the DOM rows according to the complete
         * processed result. Previously the JS sorted an Array only,
         * but the HTML table stayed in its old order. That caused
         * strange visible sequences such as 5,4,3,2,1 or mixed pages.
         */
        processedRows.forEach(row => {
            tableBody.appendChild(row);
        });

        // Keep the empty row as the final row.
        tableBody.appendChild(emptyRow);

        // 3. KEEP THE ORIGINAL NO
        // NO is a fixed display number assigned when the page first loads.
        // Search, Filter, Sort and Pagination must never change it.
        processedRows.forEach(row => {
            const rowNumber =
                row.querySelector(".row-number");

            if (rowNumber) {
                rowNumber.textContent =
                    row.dataset.originalNo || "";
            }
        });

        // NO MATCHES
        if (totalItems === 0) {
            currentPage = 1;

            emptyRow.hidden = false;

            paginationSummary.textContent =
                "No matching users";

            pagination.innerHTML = "";

            return;
        }

        emptyRow.hidden = true;

        // 4. PAGINATION IS ALWAYS THE LAST STEP
        const totalPages =
            Math.ceil(totalItems / PAGE_SIZE);

        if (currentPage > totalPages) {
            currentPage = totalPages;
        }

        if (currentPage < 1) {
            currentPage = 1;
        }

        const startIndex =
            (currentPage - 1) * PAGE_SIZE;

        const endIndex =
            Math.min(
                startIndex + PAGE_SIZE,
                totalItems
            );

        const rowsForCurrentPage =
            processedRows.slice(
                startIndex,
                endIndex
            );

        rowsForCurrentPage.forEach(row => {
            row.hidden = false;
        });

        paginationSummary.textContent =
            `Showing ${startIndex + 1}–${endIndex} of ${totalItems}`;

        renderPagination(totalPages);
    }

    function renderPagination(totalPages) {
        pagination.innerHTML = "";

        if (totalPages <= 1) {
            return;
        }

        pagination.appendChild(
            createPaginationButton(
                "‹",
                currentPage - 1,
                currentPage === 1,
                false,
                "Previous page"
            )
        );

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
            document.createElement("button");

        button.type = "button";
        button.className = "page-button";
        button.textContent = text;
        button.disabled = disabled;

        button.setAttribute(
            "aria-label",
            label
        );

        if (active) {
            button.classList.add("active");

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

                currentPage = page;
                renderUsers();
            }
        );

        return button;
    }

    function openFilter() {
        filterPanel.hidden = false;

        filterTrigger.setAttribute(
            "aria-expanded",
            "true"
        );
    }

    function closeFilter() {
        filterPanel.hidden = true;

        filterTrigger.setAttribute(
            "aria-expanded",
            "false"
        );
    }
    

    const registerOverlay = document.getElementById("registerOverlay");
    const registerClose = document.getElementById("registerClose");
    const registerCancel = document.getElementById("registerCancel");
    const registerUserId = document.getElementById("registerUserId");
    const registerUsername = document.getElementById("registerUsername");
    const registerProgramSelect = document.getElementById("registerProgramSelect");
    const registerClassSelect = document.getElementById("registerClassSelect");

    const programsData = JSON.parse(
        document.getElementById("programsJson")?.textContent || "[]"
    );

    const classesData = JSON.parse(
        document.getElementById("classesJson")?.textContent || "[]"
    );

    function populatePrograms(selectedProgramId) {
        registerProgramSelect.innerHTML = "";

        programsData.forEach(p => {
            const option = document.createElement("option");
            option.value = p.ProgramId;
            option.textContent = p.ProgramName;

            if (p.ProgramId === selectedProgramId) {
                option.selected = true;
            }

            registerProgramSelect.appendChild(option);
        });
    }

    function populateClasses(programId, selectedClassId) {
        registerClassSelect.innerHTML = "";

        const filtered = classesData.filter(
            c => c.ProgramId === programId
        );

        filtered.forEach(c => {
            const option = document.createElement("option");
            option.value = c.ClassId;
            option.textContent = c.ClassName;

            if (c.ClassId === selectedClassId) {
                option.selected = true;
            }

            registerClassSelect.appendChild(option);
        });
    }

    function openRegisterProgram(row) {
        const userId = row.dataset.userId;
        const username = row.dataset.username;
        const currentProgramId = row.dataset.programId || "";
        const currentClassId = row.dataset.classId || "";

        registerUserId.value = userId;
        registerUsername.textContent = username;

        const initialProgramId =
            currentProgramId ||
            (programsData[0] ? programsData[0].ProgramId : "");

        populatePrograms(initialProgramId);
        populateClasses(initialProgramId, currentClassId);

        registerOverlay.hidden = false;
        document.body.classList.add("drawer-open");
    }

    function closeRegisterProgram() {
        registerOverlay.hidden = true;
        document.body.classList.remove("drawer-open");
    }

    registerProgramSelect.addEventListener("change", () => {
        populateClasses(registerProgramSelect.value, "");
    });

    registerClose.addEventListener("click", closeRegisterProgram);
    registerCancel.addEventListener("click", closeRegisterProgram);

    registerOverlay.addEventListener("click", event => {
        if (event.target === registerOverlay) {
            closeRegisterProgram();
        }
    });

    function updateFilterCount() {
        let count = 0;

        if (activeRole !== "all") {
            count++;
        }

        if (activeStatus !== "all") {
            count++;
        }

        /*
         * Sort counts as ONE active filter group even if
         * both Sort By and Order are changed.
         */
        if (
            activeSortBy !== "default" ||
            activeSortOrder !== "asc"
        ) {
            count++;
        }

        filterCount.textContent =
            count.toString();

        filterCount.hidden =
            count === 0;
    }

    function applyFilters() {
        activeRole =
            getCheckedValue("roleFilter") ||
            "all";

        activeStatus =
            getCheckedValue("statusFilter") ||
            "all";

        activeSortBy =
            getCheckedValue("sortByFilter") ||
            "default";

        activeSortOrder =
            getCheckedValue("sortOrderFilter") ||
            "asc";

        currentPage = 1;

        updateFilterCount();
        renderUsers();
        closeFilter();
    }

    function resetFilters() {
        activeRole = "all";
        activeStatus = "all";

        activeSortBy = "default";
        activeSortOrder = "asc";

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

        currentPage = 1;

        updateFilterCount();
        renderUsers();
    }

    function openUserDetail(row) {
        const username =
            row.dataset.username || "-";

        setText(
            "detailUserId",
            row.dataset.userId || "-"
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
            row.dataset.email || "-"
        );

        setText(
            "detailPhone",
            row.dataset.phone || "-"
        );

        setText(
            "detailAddress",
            row.dataset.address || "-"
        );

        setText(
            "detailGender",
            row.dataset.gender || "-"
        );

        setText(
            "detailPosition",
            row.dataset.position || "-"
        );

        setText(
            "detailRole",
            row.dataset.position || "-"
        );

        setText(
            "detailStatus",
            row.dataset.status || "-"
        );

        showAvatar(
            row.dataset.avatar
        );

        detailOverlay.hidden = false;

        document.body.classList.add(
            "drawer-open"
        );
    }

    function closeUserDetail() {
        detailOverlay.hidden = true;

        document.body.classList.remove(
            "drawer-open"
        );
    }

    function setText(id, value) {
        const element =
            document.getElementById(id);

        if (element) {
            element.textContent = value;
        }
    }

    function showAvatar(avatarValue) {
        const image =
            document.getElementById(
                "detailAvatar"
            );

        const fallback =
            document.getElementById(
                "detailAvatarFallback"
            );

        const username =
            document.getElementById(
                "detailUsername"
            )?.textContent || "?";

        image.hidden = true;
        image.removeAttribute("src");

        fallback.hidden = false;

        fallback.textContent =
            username
                .trim()
                .charAt(0)
                .toUpperCase() || "👤";

        if (
            !avatarValue ||
            avatarValue === "-" ||
            avatarValue.toLowerCase() === "null"
        ) {
            return;
        }

        const candidates = [];

        if (
            avatarValue.startsWith("/") ||
            avatarValue.startsWith("http://") ||
            avatarValue.startsWith("https://")
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

        let candidateIndex = 0;

        const tryNext = () => {
            if (
                candidateIndex >=
                candidates.length
            ) {
                image.hidden = true;
                fallback.hidden = false;
                return;
            }

            image.src =
                candidates[candidateIndex];

            candidateIndex++;
        };

        image.onload = () => {
            image.hidden = false;
            fallback.hidden = true;
        };

        image.onerror = tryNext;

        tryNext();
    }

    async function promoteTeacher(row) {
        const id =
            row.dataset.userId;

        const username =
            row.dataset.username;

        const confirmed = confirm(
            `Promote ${username} to Teacher?`
        );

        if (!confirmed) {
            return;
        }

        try {
            const response = await fetch(
                `/Admin/PromoteToTeacher?id=${encodeURIComponent(id)}`,
                {
                    method: "POST"
                }
            );

            if (!response.ok) {
                const message =
                    await response.text();

                alert(message);
                return;
            }

            alert(
                `${username} has been promoted to Teacher.`
            );

            window.location.reload();
        }
        catch (error) {
            console.error(error);

            alert(
                "Unable to promote user to Teacher."
            );
        }
    }

    async function cancelTeacher(row) {
        const id =
            row.dataset.userId;

        const username =
            row.dataset.username;

        const confirmed = confirm(
            `Remove Teacher role from ${username}?`
        );

        if (!confirmed) {
            return;
        }

        try {
            const response = await fetch(
                `/Admin/CancelTeacherRole?id=${encodeURIComponent(id)}`,
                {
                    method: "POST"
                }
            );

            if (!response.ok) {
                const message =
                    await response.text();

                alert(message);
                return;
            }

            alert(
                `${username} is now a Student.`
            );

            window.location.reload();
        }
        catch (error) {
            console.error(error);

            alert(
                "Unable to remove Teacher role."
            );
        }
    }

    refreshUsers.addEventListener(
        "click",
        () => {
            window.location.reload();
        }
    );

    /*
     * Search is live.
     * It uses the current filters, processes ALL matches,
     * renumbers them, then paginates.
     */
    searchInput.addEventListener(
        "input",
        () => {
            currentPage = 1;
            renderUsers();
        }
    );

    filterTrigger.addEventListener(
        "click",
        event => {
            event.stopPropagation();

            if (filterPanel.hidden) {
                openFilter();
            }
            else {
                closeFilter();
            }
        }
    );

    filterClose.addEventListener(
        "click",
        closeFilter
    );

    filterApply.addEventListener(
        "click",
        applyFilters
    );

    filterReset.addEventListener(
        "click",
        resetFilters
    );

    document.addEventListener(
        "click",
        event => {
            if (
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

    tableBody.addEventListener(
        "click",
        event => {
            const actionButton =
                event.target.closest(
                    "[data-action]"
                );

            if (!actionButton) {
                return;
            }

            const row =
                actionButton.closest(
                    ".user-row"
                );

            if (!row) {
                return;
            }

            const action =
                actionButton.dataset.action;

            if (action === "detail") {
                openUserDetail(row);
            }
            else if (
                action === "promote"
            ) {
                promoteTeacher(row);
            }
            else if (
                action === "cancel-teacher"
            ) {
                cancelTeacher(row);
            }
            else if (action === "register-program") {
                openRegisterProgram(row);
            }
        }
    );

    detailClose.addEventListener(
        "click",
        closeUserDetail
    );

    detailOverlay.addEventListener(
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

    document.addEventListener(
        "keydown",
        event => {
            if (event.key === "Escape") {
                if (
                    !detailOverlay.hidden
                ) {
                    closeUserDetail();
                }

                if (
                    !filterPanel.hidden
                ) {
                    closeFilter();
                }
                if (!registerOverlay.hidden) {
                    closeRegisterProgram();
                }
            }
        }
    );

    updateFilterCount();
    renderUsers();
});
