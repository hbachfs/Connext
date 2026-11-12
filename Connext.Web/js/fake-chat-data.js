// Dữ liệu mẫu: khi có API thật thì xóa file này
const fakeConversations = {
    'test': {
        name: 'Test', letter: 'A', avatarClass: '', online: true, status: 'Đang hoạt động', unread: 0,
        messages: [
            { mine: false, content: 'Chào bạn! Dự án web chat tới đâu rồi?', time: '13:10' },
            { mine: true, content: 'Mình vừa xong trang đăng nhập, đang làm giao diện chat 😄', time: '13:13' }
        ]
    },
    'hoang-long': {
        name: 'Hoàng Long', letter: 'H', avatarClass: 'teal', online: false, status: 'Hoạt động 2 giờ trước', unread: 0,
        messages: [
            { mine: false, content: 'Chào bạn, hôm nay nhóm mình họp lúc mấy giờ nhỉ?', time: '08:30' },
            { mine: true, content: 'Chào bạn! Hình như 9 giờ, để mình xem lại.', time: '08:32' },
            { mine: true, content: 'Đúng rồi, 9 giờ trên Google Meet nha.', time: '08:33' },
            { mine: false, content: 'Oke, cảm ơn bạn nhiều.', time: '08:34' },
            { mine: false, content: 'Bạn nhớ mang theo file Figma mới nhé.', time: '08:34' },
            { mine: true, content: 'Ừ, mình có link rồi.', time: '08:36' },
            { mine: false, content: 'Bên bạn đang làm tới phần nào rồi?', time: '09:45' },
            { mine: true, content: 'Mình làm xong trang danh bạ rồi á.', time: '09:47' },
            { mine: true, content: 'Còn hộp tạo nhóm với thêm bạn cũng xong luôn.', time: '09:47' },
            { mine: false, content: 'Nhanh vậy! Nhìn có giống Figma không?', time: '09:50' },
            { mine: true, content: 'Giống lắm, mình canh từng icon luôn đó 😄', time: '09:52' },
            { mine: false, content: 'Hay quá. Tối nay mình ráp thử với API đăng nhập nha.', time: '09:55' },
            { mine: true, content: 'Được á, bạn gửi mình đường dẫn API nhé.', time: '09:56' },
            { mine: false, content: 'Để mình hỏi lại bạn làm BE.', time: '10:02' },
            { mine: false, content: 'Bạn ấy nói chiều nay mới đưa lên server được.', time: '10:15' },
            { mine: true, content: 'Vậy chiều mình test luôn.', time: '10:18' },
            { mine: false, content: 'Phần tin nhắn dùng SignalR đúng không?', time: '10:40' },
            { mine: true, content: 'Đúng rồi, thầy yêu cầu phải có đã xem nữa.', time: '10:42' },
            { mine: false, content: 'Còn phân trang tin nhắn thì sao?', time: '10:43' },
            { mine: true, content: 'Mỗi lần lấy 10 tin, cuộn lên đỉnh thì tải thêm.', time: '10:45' },
            { mine: false, content: 'À mình hiểu rồi.', time: '10:46' },
            { mine: false, content: 'Vậy tập tin thì chia làm hai loại hả?', time: '10:46' },
            { mine: true, content: 'Ừ, ảnh thì hiện ảnh, file khác chỉ hiện tên và link tải.', time: '10:49' },
            { mine: false, content: 'Oke, để mình ghi vào báo cáo.', time: '10:52' },
            { mine: true, content: 'Cảm ơn bạn nha.', time: '10:53' },
            { mine: false, content: 'Trưa nay bạn ăn gì chưa?', time: '11:40' },
            { mine: true, content: 'Chưa, mình làm xong đoạn này rồi đi ăn.', time: '11:44' },
            { mine: false, content: 'Nhớ nghỉ tay nha, làm từ sáng tới giờ rồi.', time: '11:45' },
            { mine: true, content: 'Hihi, cảm ơn bạn.', time: '11:47' },
            { mine: false, content: 'Chiều nay bạn có lên trường không?', time: '12:30' },
            { mine: true, content: 'Có á, 2 giờ mình có mặt.', time: '12:35' },
            { mine: false, content: 'Vậy gặp nhau ở thư viện nhé.', time: '12:36' },
            { mine: true, content: 'Oke bạn.', time: '12:37' },
            { mine: false, content: 'Chiều mình gửi bạn file báo cáo nha.', time: '12:40' },
            { mine: true, content: 'Oke bạn, mình đợi.', time: '12:41' }
        ]
    },
    'minh-anh': {
        name: 'Minh Anh', letter: 'A', avatarClass: '', online: true, status: 'Đang hoạt động', unread: 2,
        messages: [
            { mine: true, content: 'Bạn ơi, mình làm xong giao diện danh bạ rồi nè.', time: '09:30' },
            { mine: false, content: 'Ồ nhanh vậy!', time: '09:40' },
            { mine: false, content: 'Tuyệt quá! Gửi mình xem thử với nhé', time: '09:41' }
        ]
    },
    'thiet-ke': {
        name: 'Nhóm Thiết kế', letter: 'K', avatarClass: 'dark', online: false, status: '5 thành viên', unread: 5,
        messages: [
            { mine: true, content: 'Mọi người xem giúp mình màu nút đăng nhập với.', time: '07:50' },
            { mine: false, content: 'Mình thấy ổn rồi đó.', time: '08:05' },
            { mine: false, content: 'Chữ trên nút hơi nhỏ thôi.', time: '08:07' },
            { mine: false, content: 'Tăng lên 14px là đẹp.', time: '08:08' },
            { mine: false, content: 'Icon thì giữ nguyên nha.', time: '08:12' },
            { mine: false, content: 'Mình sẽ cập nhật file Figma trong hôm nay', time: '08:15' }
        ]
    }
};

for (const id in fakeConversations) {
    fakeConversations[id].messages = fakeConversations[id].messages.map((m, i) => ({ ...m, id: i + 1 }));
}