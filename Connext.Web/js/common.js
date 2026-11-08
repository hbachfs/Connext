document.querySelectorAll('.toggle-pass').forEach(btn => {
	btn.addEventListener('click', () => {
		const input = btn.parentElement.querySelector('input');
		const icon = btn.querySelector('i');
		if (input.type === 'password') {
			input.type = 'text';
			icon.className = 'bi bi-eye-slash';
			} else {
				input.type = 'password';
				icon.className = 'bi bi-eye';
			}
	});
});

document.querySelectorAll('.tab-pill').forEach(pill => {
	pill.addEventListener('click', () => {
		pill.parentElement.querySelectorAll('.tab-pill').forEach(p => {
			p.classList.remove('active');
		});
		pill.classList.add('active');
	});
});
function openModal(id) {
	const modal = document.getElementById(id);
	modal.classList.remove('hidden');
	modal.querySelector('input').focus();
}

function closeModal(e) {
	if (e.target === e.currentTarget) {
		e.currentTarget.classList.add('hidden');
	}
}

function handleAddFriend() { /* BE gửi lời mời kết bạn */ }
function hideModal(id) {
	document.getElementById(id).classList.add('hidden');
}

function handleCreateGroup() { /* BE tạo nhóm với các bạn đã chọn */ }

function toggleSettingsMenu(e) {
	e.preventDefault();
	document.getElementById('settingsMenu').classList.toggle('hidden');
}

document.addEventListener('click', e => {
	const menu = document.getElementById('settingsMenu');
	if (menu && !e.target.closest('.sidebar-settings')) {
		menu.classList.add('hidden');
	}
});

function handleLogout() {
	/* BE xóa token đăng nhập ở đây */
	window.location.href = 'login.html';
}