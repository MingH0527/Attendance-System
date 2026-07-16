
async function loadNonTeacherUsers() {
    const response = await fetch('/Admin/GetNonTeacherUsers');
    const users = await response.json();

    const container = document.getElementById('nonTeacherList');
    container.innerHTML = '';

    users.forEach(user => {
        const item = document.createElement('div');
        item.textContent = user.teacherName;

        const btn = document.createElement('button');
        btn.textContent = '设为老师';
        btn.onclick = () => promoteToTeacher(user.teacherId);

        item.appendChild(btn);
        container.appendChild(item);
    });
}


async function promoteToTeacher(userId) {
    const response = await fetch(`/Admin/PromoteToTeacher?id=${userId}`, {
        method: 'POST'
    });

    if (response.ok) {
        alert('已设为老师');
        loadNonTeacherUsers(); 
    } else {
        const error = await response.text();
        alert('操作失败: ' + error);
    }
}
loadNonTeacherUsers();