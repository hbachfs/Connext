# Thiết kế UX - Connext

Connext là ứng dụng web nhắn tin trực tuyến. Người dùng đăng ký và đăng nhập bằng số điện thoại, kết bạn, nhắn tin và gửi file, tạo nhóm chat, gọi thoại hoặc gọi video, và quản lý hồ sơ cá nhân. Thư mục này chứa toàn bộ sản phẩm UX bàn giao cho nhóm gồm Sitemap, User Flow và Wireframe. Các sản phẩm đều được vẽ theo giao diện hoàn chỉnh mà UI Designer đã thiết kế trên Figma, nên chỉ gồm những màn hình và thao tác đang có trong giao diện.

## Sản phẩm bàn giao

Sitemap là sơ đồ cây cho biết ứng dụng có những màn hình nào và chúng thuộc nhóm nào. User Flow là các sơ đồ mô tả từng bước người dùng đi qua để hoàn thành một việc, kèm các điểm rẽ nhánh, ví dụ luồng đăng ký hoặc luồng kết bạn. Wireframe là bản phác thảo bố cục màu xám của từng màn hình, chỉ thể hiện vị trí các thành phần và nội dung chữ, chưa có màu thương hiệu, ảnh hay icon thật.

## Mục lục sơ đồ

| Mã | Sơ đồ | Nội dung | File xuất |
|---|---|---|---|
| UX-00 | Sitemap | Cây các màn hình: Khách, Chat, Danh bạ, Cài đặt, Cuộc gọi | `sitemap/UX-00_Sitemap_v1.png` |
| UX-01 | Flow Đăng ký | Điền họ tên, SĐT, email, mật khẩu rồi tạo tài khoản | `user-flow/UX-01_Flow-Dang-ky_v1.png` |
| UX-02 | Flow Đăng nhập | Đăng nhập bằng SĐT và mật khẩu, rẽ sang Quên mật khẩu hoặc Đăng ký | `user-flow/UX-02_Flow-Dang-nhap_v1.png` |
| UX-03 | Flow Quên mật khẩu | Nhập email, nhập mã 6 số, đặt mật khẩu mới | `user-flow/UX-03_Flow-Quen-mat-khau_v1.png` |
| UX-04 | Flow Kết bạn | Người gửi mời bằng SĐT, người nhận chấp nhận hoặc xóa trong tab Lời mời | `user-flow/UX-04_Flow-Ket-ban_v1.png` |
| UX-05 | Flow Tạo nhóm | Nhập tên nhóm, chọn bạn, tạo nhóm | `user-flow/UX-05_Flow-Tao-nhom_v1.png` |
| UX-06 | Flow Nhắn tin và gọi | Chọn hội thoại, gửi tin và file, menu hội thoại, gọi thoại hoặc video | `user-flow/UX-06_Flow-Nhan-tin-va-goi_v1.png` |
| UX-07 | Flow Hồ sơ và đổi mật khẩu | Cập nhật hồ sơ, đổi mật khẩu, đăng xuất | `user-flow/UX-07_Flow-Ho-so-va-doi-mat-khau_v1.png` |

## Wireframe

Wireframe gồm 16 màn hình, vẽ lại từ giao diện Figma của UI Designer theo phong cách xám độ trung thực thấp. Toàn bộ nằm trong một file Figma, mỗi màn hình là một frame có thể chỉnh sửa, và dùng chung các component Button, Input, Pill, Icon Button, Avatar đặt trong khung `Components` ở đầu trang.

Link Figma: https://www.figma.com/design/1TfPer6pXaRggbWg6PeOzJ

| Mã | Màn hình | Nội dung | Nhóm |
|---|---|---|---|
| WF-01 | Đăng nhập | Nhập SĐT và mật khẩu, link Quên mật khẩu và Đăng ký | Khách |
| WF-02 | Đăng ký | Họ tên, SĐT, email, mật khẩu, nhập lại mật khẩu | Khách |
| WF-03 | Quên mật khẩu | Nhập email để nhận mã khôi phục | Khách |
| WF-04 | Nhập mã | 6 ô nhập mã, nút Xác thực, Gửi lại mã, Thử với email khác | Khách |
| WF-05 | Đặt lại mật khẩu | Nhập mật khẩu mới và nhập lại | Khách |
| WF-06 | Trò chuyện | Danh sách hội thoại có tab Tất cả, Chưa đọc, Đã đọc; khung chat, ô nhập tin, nút đính kèm | Chat |
| WF-07 | Chưa có bạn | Trạng thái trống khi chưa có hội thoại | Chat |
| WF-08 | Bạn bè | Tab, sắp xếp A-Z, thẻ bạn bè (Nhắn tin, Hủy kết bạn), phân trang | Danh bạ |
| WF-09 | Hồ sơ | Cập nhật hồ sơ cá nhân và đổi mật khẩu | Cài đặt |
| WF-10 | Mời kết bạn | Popup nhập SĐT và Gửi lời mời | Chat |
| WF-11 | Tạo nhóm | Popup nhập tên nhóm, tìm và chọn bạn, Hủy hoặc Tạo nhóm | Chat |
| WF-12 | Menu hội thoại | Ghim hội thoại, Thêm vào nhóm, Xóa hội thoại | Chat |
| WF-13 | Menu cài đặt | Hồ sơ, Đăng xuất | Cài đặt |
| WF-14 | Lời mời kết bạn | Thẻ lời mời với số bạn chung, nút Chấp nhận và Xóa | Danh bạ |
| WF-15 | Gọi thoại | Đồng hồ cuộc gọi, avatar, nút mic, camera, loa, kết thúc | Cuộc gọi |
| WF-16 | Gọi video | Như gọi thoại, thêm khung hình của bản thân | Cuộc gọi |

## Cấu trúc thư mục

```
design/ux-wireframe/
  README.md
  sitemap/
    UX-00_Sitemap.png
  user-flow/
    UX-01_Flow-Dang-ky.png
    UX-02_Flow-Dang-nhap.png
    UX-03_Flow-Quen-mat-khau.png
    UX-04_Flow-Ket-ban.png
    UX-05_Flow-Tao-nhom.png
    UX-06_Flow-Nhan-tin-va-goi.png
    UX-07_Flow-Ho-so-va-doi-mat-khau.png
  wireframe/
    UX-Wireframe.pdf
```
