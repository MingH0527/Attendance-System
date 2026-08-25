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

    allRows.forEach((row, index) => {
        row.dataset.originalIndex = index.toString();
    });

    let currentPage = 1;
    let activeRole = "all";
    let activeStatus = "all";
    let activeSort = "default";

    function getCheckedValue(name) {
        const checked = document.querySelector(
            `input[name="${name}"]:checked`
        );

        return checked ? checked.value : "all";
    }

    function setCheckedValue(name, value) {
        const input = document.querySelector(
            `input[name="${name}"][value="${value}"]`
        );

        if (input) {
            input.checked = true;
        }
    }

    function getFilteredRows() {
        const searchText = searchInput.value
            .trim()
            .toLowerCase();

        let rows = allRows.filter(row => {
            const username = (row.dataset.username || "")
                .toLowerCase();

            const role = row.dataset.position || "";
            const status = row.dataset.status || "";

            const matchesUsername =
                username.includes(searchText);

            const matchesRole =
                activeRole === "all" ||
                role === activeRole;

            const matchesStatus =
                activeStatus === "all" ||
                status === activeStatus;

            return matchesUsername &&
                matchesRole &&
                matchesStatus;
        });

        if (activeSort === "az") {
            rows.sort((a, b) =>
                (a.dataset.username || "").localeCompare(
                    b.dataset.username || "",
                    undefined,
                    { sensitivity: "base" }
                )
            );
        }
        else if (activeSort === "za") {
            rows.sort((a, b) =>
                (b.dataset.username || "").localeCompare(
                    a.dataset.username || "",
                    undefined,
                    { sensitivity: "base" }
                )
            );
        }
        else {
            rows.sort(
                (a, b) =>
                    Number(a.dataset.originalIndex) -
                    Number(b.dataset.originalIndex)
            );
        }

        return rows;
    }

    function renderUsers() {
        const filteredRows = getFilteredRows();
        const totalItems = filteredRows.length;
        const totalPages = Math.max(
            1,
            Math.ceil(totalItems / PAGE_SIZE)
        );

        if (currentPage > totalPages) {
            currentPage = totalPages;
        }

        if (currentPage < 1) {
            currentPage = 1;
        }

        allRows.forEach(row => {
            row.hidden = true;
        });

        if (totalItems === 0) {
            emptyRow.hidden = false;
            paginationSummary.textContent = "0 users";
            pagination.innerHTML = "";
            return;
        }

        emptyRow.hidden = true;

        const startIndex =
            (currentPage - 1) * PAGE_SIZE;

        const endIndex =
            Math.min(startIndex + PAGE_SIZE, totalItems);

        const rowsForCurrentPage =
            filteredRows.slice(startIndex, endIndex);

        rowsForCurrentPage.forEach((row, pageIndex) => {
            row.hidden = false;

            const rowNumber =
                row.querySelector(".row-number");

            if (rowNumber) {
                rowNumber.textContent =
                    (startIndex + pageIndex + 1).toString();
            }
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

        for (let page = 1; page <= totalPages; page++) {
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
        button.setAttribute("aria-label", label);

        if (active) {
            button.classList.add("active");
            button.setAttribute("aria-current", "page");
        }

        button.addEventListener("click", () => {
            if (disabled || page === currentPage) {
                return;
            }

            currentPage = page;
            renderUsers();
        });

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

    function updateFilterCount() {
        let count = 0;

        if (activeRole !== "all") {
            count++;
        }

        if (activeStatus !== "all") {
            count++;
        }

        if (activeSort !== "default") {
            count++;
        }

        filterCount.textContent = count.toString();
        filterCount.hidden = count === 0;
    }

    function applyFilters() {
        activeRole =
            getCheckedValue("roleFilter");

        activeStatus =
            getCheckedValue("statusFilter");

        activeSort =
            getCheckedValue("sortFilter");

        currentPage = 1;

        updateFilterCount();
        renderUsers();
        closeFilter();
    }

    function resetFilters() {
        activeRole = "all";
        activeStatus = "all";
        activeSort = "default";

        setCheckedValue("roleFilter", "all");
        setCheckedValue("statusFilter", "all");
        setCheckedValue("sortFilter", "default");

        currentPage = 1;

        updateFilterCount();
        renderUsers();
    }

    function openUserDetail(row) {
        const username =
            row.dataset.username || "-";

        setText("detailUsername", username);
        setText("detailUsernameValue", username);
        setText("detailEmail", row.dataset.email || "-");
        setText("detailPhone", row.dataset.phone || "-");
        setText("detailAddress", row.dataset.address || "-");
        setText("detailGender", row.dataset.gender || "-");
        setText("detailPosition", row.dataset.position || "-");
        setText("detailRole", row.dataset.position || "-");
        setText("detailStatus", row.dataset.status || "-");

        showAvatar(row.dataset.avatar);

        detailOverlay.hidden = false;
        document.body.classList.add("drawer-open");
    }

    function closeUserDetail() {
        detailOverlay.hidden = true;
        document.body.classList.remove("drawer-open");
    }

    function setText(id, value) {
        const element = document.getElementById(id);

        if (element) {
            element.textContent = value;
        }
    }

    function showAvatar(avatarValue) {
        const image =
            document.getElementById("detailAvatar");

        const fallback =
            document.getElementById("detailAvatarFallback");

        const username =
            document.getElementById("detailUsername")?.textContent || "?";

        image.hidden = true;
        image.removeAttribute("src");

        fallback.hidden = false;
        fallback.textContent =
            username.trim().charAt(0).toUpperCase() || "👤";

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
            candidates.push(avatarValue);
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
            if (candidateIndex >= candidates.length) {
                image.hidden = true;
                fallback.hidden = false;
                return;
            }

            image.src = candidates[candidateIndex];
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
        const id = row.dataset.userId;
        const username = row.dataset.username;

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
                const message = await response.text();
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
        const id = row.dataset.userId;
        const username = row.dataset.username;

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
                const message = await response.text();
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

    refreshUsers.addEventListener("click", () => {
        window.location.reload();
    });

    searchInput.addEventListener("input", () => {
        currentPage = 1;
        renderUsers();
    });

    filterTrigger.addEventListener("click", event => {
        event.stopPropagation();

        if (filterPanel.hidden) {
            openFilter();
        }
        else {
            closeFilter();
        }
    });

    filterClose.addEventListener("click", closeFilter);
    filterApply.addEventListener("click", applyFilters);
    filterReset.addEventListener("click", resetFilters);

    document.addEventListener("click", event => {
        if (
            !filterPanel.hidden &&
            !filterPanel.contains(event.target) &&
            !filterTrigger.contains(event.target)
        ) {
            closeFilter();
        }
    });

    tableBody.addEventListener("click", event => {
        const actionButton =
            event.target.closest("[data-action]");

        if (!actionButton) {
            return;
        }

        const row =
            actionButton.closest(".user-row");

        if (!row) {
            return;
        }

        const action =
            actionButton.dataset.action;

        if (action === "detail") {
            openUserDetail(row);
        }
        else if (action === "promote") {
            promoteTeacher(row);
        }
        else if (action === "cancel-teacher") {
            cancelTeacher(row);
        }
    });

    detailClose.addEventListener(
        "click",
        closeUserDetail
    );

    detailOverlay.addEventListener(
        "click",
        event => {
            if (event.target === detailOverlay) {
                closeUserDetail();
            }
        }
    );

    document.addEventListener("keydown", event => {
        if (event.key === "Escape") {
            if (!detailOverlay.hidden) {
                closeUserDetail();
            }

            if (!filterPanel.hidden) {
                closeFilter();
            }
        }
    });

    updateFilterCount();
    renderUsers();
});
