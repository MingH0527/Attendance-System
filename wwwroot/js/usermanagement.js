const searchInput = document.getElementById("userSearch");
const roleFilter = document.getElementById("roleFilter");
const table = document.getElementById("userTable");

function filterUsers() {

    const searchText =
        searchInput.value.toLowerCase().trim();

    const selectedRole =
        roleFilter.value.toLowerCase();

    const rows =
        table.querySelectorAll("tbody tr");

    rows.forEach(row => {

        // Skip "No users found" row
        if (row.cells.length < 8) {
            return;
        }

        const username =
            row.cells[1].textContent.toLowerCase();

        const email =
            row.cells[2].textContent.toLowerCase();

        const phone =
            row.cells[3].textContent.toLowerCase();

        const position =
            row.cells[5].textContent.toLowerCase();

        const matchSearch =
            username.includes(searchText) ||
            email.includes(searchText) ||
            phone.includes(searchText);

        const matchRole =
            selectedRole === "" ||
            position === selectedRole;

        if (matchSearch && matchRole) {
            row.style.display = "";
        }
        else {
            row.style.display = "none";
        }
    });
}

searchInput.addEventListener("input", filterUsers);

roleFilter.addEventListener("change", filterUsers);