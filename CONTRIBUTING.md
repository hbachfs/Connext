# Hướng dẫn đóng góp cho Connext

## Quy tắc vàng

1. **Không push thẳng vào `main` hoặc `develop`.** Mọi thay đổi đi qua Pull Request (PR).
2. Mỗi PR nhỏ, làm một việc, dễ review. Mục tiêu dưới 400 dòng thay đổi.
3. Có người review duyệt mới được merge. Người viết không tự duyệt PR của mình.
4. Trong lúc làm, thấy ngoài phạm vi SRS thì hỏi PM trước, không tự thêm tính năng.

## Quy trình hằng ngày

```bash
git checkout develop
git pull origin develop
git checkout -b feature/be1-chat-hub   # <loại>/<vai-trò>-<mô-tả-ngắn>
# ... viết code ...
git add .
git commit -m "feat(chat): add SendMessage hub method"
git push -u origin feature/be1-chat-hub
```

Sau đó mở Pull Request vào `develop` trên GitHub, điền theo mẫu có sẵn.

## Đặt tên nhánh

`<loại>/<vai-trò>-<mô-tả-ngắn>` bằng chữ thường, nối bằng dấu gạch ngang.
Ví dụ: `feature/fe1-message-bubble`, `qa/qa2-postman-auth`, `devops/ops-hardening`, `docs/pm-backend-decision`.

Không đặt nhánh dạng `a/b/c` khi nhánh `a/b` đã tồn tại: git không cho tạo (ví dụ nhánh
`feature/be1-core-realtime` đã có thì không tạo được `feature/be1-core-realtime/chat-hub`).

## Quy tắc commit

Dạng `loại(phạm vi): mô tả ngắn`, viết ở thì hiện tại.

| Loại | Dùng khi |
|---|---|
| `feat` | thêm tính năng |
| `fix` | sửa lỗi |
| `docs` | tài liệu |
| `test` | thêm hoặc sửa test |
| `chore` | cấu hình, build, CI |
| `refactor` | đổi cấu trúc code, không đổi hành vi |

## Quy tắc Pull Request

- Tiêu đề rõ ràng, nêu tính năng liên quan (ví dụ `FR-05`) và gắn Issue (`Closes #12`).
  Dùng `Refs #12` khi PR chỉ làm một phần của issue. Vì PR vào `develop` (không phải nhánh mặc định `main`),
  GitHub **không tự đóng** issue khi merge: người tạo PR đóng issue thủ công kèm comment sau khi merge.
- Build phải qua, tự chạy thử trước khi xin review.
- Người review phản hồi trong vòng 24 giờ.
- Dùng **Squash and merge** để lịch sử `develop` gọn. Riêng PR `develop` vào `main` dùng **Create a merge commit** để hai nhánh không bị lệch lịch sử.
- Cuối mỗi Sprint, PM và DevOps merge `develop` vào `main`.

## Quy ước code C#

- Tuân theo quy ước đặt tên của .NET: `PascalCase` cho lớp và phương thức, `_camelCase` cho trường private.
- DTO, Enum và quy tắc kiểm tra dữ liệu dùng chung đặt trong `Connext.Shared`.
- Không đưa mật khẩu, khóa API, chuỗi kết nối thật vào repo. Dùng biến môi trường hoặc User Secrets.

## Báo lỗi

Tạo Issue trên GitHub, ghi: các bước tái hiện, kết quả mong đợi, kết quả thực tế, ảnh chụp màn hình và trình duyệt đang dùng. QA gắn nhãn mức độ (`critical`, `major`, `minor`).
