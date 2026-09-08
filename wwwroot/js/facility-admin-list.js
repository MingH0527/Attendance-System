document.addEventListener("DOMContentLoaded", () => {
    document.querySelectorAll("[data-facility-admin-list]").forEach(root => {
        const rows = Array.from(root.querySelectorAll("[data-list-row]"));
        const body = root.querySelector("[data-list-body]");
        const emptyRow = root.querySelector("[data-empty-row]");
        const search = root.querySelector("[data-list-search]");
        const pagination = root.querySelector("[data-pagination]");
        const summary = root.querySelector("[data-pagination-summary]");
        const filterToggle = root.querySelector("[data-filter-toggle]");
        const filterPanel = root.querySelector("[data-filter-panel]");
        const filterClose = root.querySelector("[data-filter-close]");
        const filterApply = root.querySelector("[data-filter-apply]");
        const filterReset = root.querySelector("[data-filter-reset]");
        const statusSelect = root.querySelector("[data-filter-status]");
        const facilitySelect = root.querySelector("[data-filter-facility]");
        const sortSelect = root.querySelector("[data-filter-sort]");
        const pageSize = Number(root.dataset.pageSize || 5);

        let currentPage = 1;
        let status = "all";
        let facility = "all";
        let sort = "default";

        function processedRows() {
            const term = (search?.value || "").trim().toLowerCase();
            const result = rows.filter(row => {
                const matchesSearch = !term || (row.dataset.search || "").includes(term);
                const matchesStatus = status === "all" || row.dataset.status === status;
                const matchesFacility = facility === "all" || row.dataset.facility === facility;
                return matchesSearch && matchesStatus && matchesFacility;
            });

            result.sort((a, b) => {
                if (sort === "date-asc") return (a.dataset.date || "").localeCompare(b.dataset.date || "");
                if (sort === "date-desc") return (b.dataset.date || "").localeCompare(a.dataset.date || "");
                if (sort === "student-asc") return (a.dataset.student || "").localeCompare(b.dataset.student || "", undefined, { sensitivity: "base" });
                return rows.indexOf(a) - rows.indexOf(b);
            });
            return result;
        }

        function renderPagination(totalPages) {
            if (!pagination) return;
            pagination.innerHTML = "";
            if (totalPages <= 1) return;

            const makeButton = (label, page, disabled = false, active = false) => {
                const button = document.createElement("button");
                button.type = "button";
                button.textContent = label;
                button.disabled = disabled;
                if (active) button.classList.add("active");
                button.addEventListener("click", () => { currentPage = page; render(); });
                pagination.appendChild(button);
            };

            makeButton("‹", Math.max(1, currentPage - 1), currentPage === 1);
            for (let page = 1; page <= totalPages; page++) makeButton(String(page), page, false, page === currentPage);
            makeButton("›", Math.min(totalPages, currentPage + 1), currentPage === totalPages);
        }

        function render() {
            const processed = processedRows();
            const total = processed.length;
            const totalPages = Math.max(1, Math.ceil(total / pageSize));
            if (currentPage > totalPages) currentPage = totalPages;

            rows.forEach(row => row.hidden = true);
            processed.forEach(row => body?.appendChild(row));
            if (emptyRow && body) body.appendChild(emptyRow);

            const start = (currentPage - 1) * pageSize;
            const visible = processed.slice(start, start + pageSize);
            visible.forEach(row => row.hidden = false);
            if (emptyRow) emptyRow.hidden = total !== 0;

            if (summary) {
                summary.textContent = total === 0
                    ? "0 records"
                    : `Showing ${start + 1}–${Math.min(start + pageSize, total)} of ${total}`;
            }
            renderPagination(totalPages);
        }

        search?.addEventListener("input", () => { currentPage = 1; render(); });
        filterToggle?.addEventListener("click", () => { if (filterPanel) filterPanel.hidden = !filterPanel.hidden; });
        filterClose?.addEventListener("click", () => { if (filterPanel) filterPanel.hidden = true; });
        filterApply?.addEventListener("click", () => {
            status = statusSelect?.value || "all";
            facility = facilitySelect?.value || "all";
            sort = sortSelect?.value || "default";
            currentPage = 1;
            if (filterPanel) filterPanel.hidden = true;
            render();
        });
        filterReset?.addEventListener("click", () => {
            if (statusSelect) statusSelect.value = "all";
            if (facilitySelect) facilitySelect.value = "all";
            if (sortSelect) sortSelect.value = "default";
            if (search) search.value = "";
            status = facility = "all";
            sort = "default";
            currentPage = 1;
            render();
        });

        render();
    });
});
