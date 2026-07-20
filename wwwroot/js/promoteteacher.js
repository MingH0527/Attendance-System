document.addEventListener("DOMContentLoaded", function () {
    loadUsers();

    document.getElementById("userTableBody")
        .addEventListener("click", function (e) {
            const btn = e.target.closest(".set-teacher-btn, .cancel-teacher-btn");
            if (!btn) return;

            const userId = btn.dataset.userid;
            const isPromoting = btn.classList.contains("set-teacher-btn");
            const url = isPromoting ? "/Admin/PromoteToTeacher" : "/Admin/CancelTeacherRole";

            btn.disabled = true;

            fetch(url, {
                method: "POST",
                headers: { "Content-Type": "application/x-www-form-urlencoded" },
                body: "id=" + encodeURIComponent(userId)
            })
                .then(res => {
                    if (!res.ok) {
                        return res.text().then(text => { throw new Error(text || "操作失败"); });
                    }
                    return res;
                })
                .then(() => {
                    updateRow(btn.closest("tr"), isPromoting ? "Teacher" : "Student");
                })
                .catch(err => {
                    alert(err.message || "Failed. Please try again later.");
                    btn.disabled = false;
                });
        });

    function loadUsers() {
        fetch("/Admin/GetAllUsersWithRoles")
            .then(res => res.json())
            .then(users => {
                const tbody = document.getElementById("userTableBody");
                tbody.innerHTML = "";

                users.forEach(user => {
                    tbody.appendChild(buildRow(user));
                });
            })
            .catch(err => {
                console.error(err);
                document.getElementById("userTableBody").innerHTML =
                    "<tr><td colspan='3'>Loading failed.</td></tr>";
            });
    }

    function buildRow(user) {
        const tr = document.createElement("tr");
        tr.dataset.userid = user.userId;

        const nameTd = document.createElement("td");
        nameTd.textContent = user.username;

        const roleTd = document.createElement("td");
        roleTd.className = "role-cell";
        roleTd.textContent = user.position;

        const actionTd = document.createElement("td");
        actionTd.className = "action-cell";
        actionTd.appendChild(buildActionButton(user));

        tr.appendChild(nameTd);
        tr.appendChild(roleTd);
        tr.appendChild(actionTd);

        return tr;
    }

    function buildActionButton(user) {
        if (user.position === "Admin") {
            const span = document.createElement("span");
            span.textContent = "—";
            return span;
        }

        const btn = document.createElement("button");
        btn.dataset.userid = user.userId;

        if (user.position === "Teacher") {
            btn.className = "btn btn-cancel cancel-teacher-btn";
            btn.textContent = "Cancel Teacher Role";
        } else {
            btn.className = "btn btn-set set-teacher-btn";
            btn.textContent = "Set as Teacher";
        }

        return btn;
    }

    function updateRow(row, newPosition) {
        row.querySelector(".role-cell").textContent = newPosition;

        const actionTd = row.querySelector(".action-cell");
        actionTd.innerHTML = "";
        actionTd.appendChild(buildActionButton({
            userId: row.dataset.userid,
            position: newPosition
        }));
    }
});