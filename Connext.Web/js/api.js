// Địa chỉ backend (profile "https" trong launchSettings của API).
// Backend bật UseHttpsRedirection nên gọi qua https để tránh bị chuyển hướng làm hỏng CORS.
// Lần đầu chạy local cần: dotnet dev-certs https --trust
const API_BASE = 'https://localhost:7045';

// Gọi POST JSON tới API. Luôn trả về { ok, message, data } và không ném lỗi.
async function apiPost(path, body) {
	try {
		const res = await fetch(API_BASE + path, {
			method: 'POST',
			headers: { 'Content-Type': 'application/json' },
			body: JSON.stringify(body)
		});

		let json = null;
		try { json = await res.json(); } catch { /* phản hồi không phải JSON */ }

		// Dạng ApiResponse của BE: { success, message, data }
		if (json && typeof json.success === 'boolean') {
			return { ok: res.ok && json.success, message: json.message, data: json.data };
		}

		// Lỗi kiểm tra dữ liệu mặc định của ASP.NET: { errors: { Field: ["..."] } }
		if (json && json.errors) {
			const first = Object.values(json.errors)[0];
			return { ok: false, message: (first && first[0]) || 'Dữ liệu không hợp lệ.', data: null };
		}

		return { ok: false, message: 'Có lỗi xảy ra. Vui lòng thử lại.', data: null };
	} catch {
		return { ok: false, message: 'Không kết nối được máy chủ. Vui lòng thử lại sau.', data: null };
	}
}
