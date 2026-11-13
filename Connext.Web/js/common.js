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
	localStorage.removeItem("jwt");
	window.location.href = 'login.html';
}
// ===== Kết nối SignalR ChatHub =====

// ===== Kết nối SignalR ChatHub =====
// Yêu cầu: API_BASE đã được khai báo trước file này, và renderMessage(msg) đã tồn tại.
const chatConnection = new signalR.HubConnectionBuilder()
	.withUrl(`${API_BASE}/hubs/chat`, {
		// SignalR sẽ tự gắn token vào query string (?access_token=...) khi dùng WebSocket
		accessTokenFactory: () => localStorage.getItem("jwt") || ""
	})
	.withAutomaticReconnect()
	.build();

// ----- Nhận sự kiện từ server -----
chatConnection.on("ReceiveMessage", async (msg) => {
	// msg: { id, conversationId, senderId, mine, type, content,
	//        fileUrl, fileName, fileSize, createdAt, status }
	renderMessage(msg);

	// Báo server là client đã thật sự nhận (cho trạng thái Delivered)
	if (!msg.mine) {
		try {
			await chatConnection.invoke("AckDelivered", msg.conversationId, msg.id);
		} catch (e) { console.error(e); }
	}
});

chatConnection.on("MessagesDelivered", (conversationId, userId) => {
	// TODO: đổi trạng thái tin của mình trong hội thoại này thành "Delivered"
});

chatConnection.on("MessagesSeen", (conversationId, userId, lastMsgId) => {
	// TODO: đổi trạng thái các tin có id <= lastMsgId thành "Seen"
});

chatConnection.on("UserTyping", (conversationId, userId, isTyping) => {
	// TODO: hiện/ẩn "đang nhập..."
});

chatConnection.on("UserOnlineChanged", (userId, isOnline, lastSeen) => {
	// TODO: cập nhật chấm xanh / "hoạt động lúc ..."
});

// ----- Bắt đầu kết nối -----
async function startChatConnection() {
	if (!localStorage.getItem("jwt")) return; // chưa đăng nhập thì không kết nối
	if (chatConnection.state !== signalR.HubConnectionState.Disconnected) return;

	try {
		await chatConnection.start();
		console.log("Đã kết nối ChatHub");
	} catch (err) {
		console.error("Kết nối ChatHub thất bại:", err);
		// Token sai/hết hạn (401) thì thử lại cũng vô ích
		if (String(err).includes("401")) return;
		setTimeout(startChatConnection, 5000);
	}
}

async function stopChatConnection() {
	if (chatConnection.state !== signalR.HubConnectionState.Disconnected) {
		try { await chatConnection.stop(); } catch { }
	}
}

// ----- Gọi lên hub -----
function ensureConnected() {
	return chatConnection.state === signalR.HubConnectionState.Connected;
}

// Gửi tin văn bản
async function sendChatMessage(conversationId, content) {
	if (!ensureConnected()) return;
	await chatConnection.invoke("SendMessage", conversationId, "text", content, null, null);
}

// Gửi ảnh/file (fileUrl dạng "/uploads/<tên>" lấy từ API upload)
async function sendChatFile(conversationId, type, fileUrl, fileName, caption = null) {
	if (!ensureConnected()) return;
	await chatConnection.invoke("SendMessage", conversationId, type, caption, fileUrl, fileName);
}

async function markConversationRead(conversationId) {
	if (!ensureConnected()) return;
	await chatConnection.invoke("MarkAsRead", conversationId);
}

async function sendTyping(conversationId, isTyping) {
	if (!ensureConnected()) return;
	await chatConnection.invoke("Typing", conversationId, isTyping);
}

startChatConnection();

async function handleLogout() {
	localStorage.removeItem("jwt");
	if (chatConnection) {
		try { await chatConnection.stop(); } catch { }
		chatConnection = null;
	}
	window.location.href = 'login.html';
}

chatConnection.on("ReceiveMessage", async (msg) => {
	renderMessage(msg);
	if (!msg.mine) {
		await chatConnection.invoke("AckDelivered", msg.conversationId, msg.id);
	}
});