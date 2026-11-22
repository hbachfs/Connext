# Hướng dẫn Khôi phục Hệ thống (Disaster Recovery Guide)

Tài liệu này ghi lại các bước chi tiết để khôi phục toàn bộ cơ sở dữ liệu (`ConnextDb`) và dữ liệu lưu trữ tệp tin (MinIO) từ các bản backup lưu trữ riêng tư ngoài repository, giúp dựng lại hệ thống nhanh chóng trên môi trường mới hoặc máy chủ VPS khi xảy ra sự cố.

---

## 1. Yêu cầu chuẩn bị trước khi khôi phục
- Máy chủ đã được cài đặt sẵn **Docker** và **Docker Compose**.
- Chuẩn bị sẵn các file backup tại **thư mục lưu trữ riêng tư ngoài thư mục mã nguồn** (ví dụ: `/opt/connext-backups` trên VPS hoặc thư mục backup riêng trên máy cá nhân):
  - `connext_backup.bak` (Bản sao lưu cơ sở dữ liệu SQL Server)
  - `minio_backup.tar.gz` (Bản nén dữ liệu MinIO)
- Đảm bảo file cấu hình bảo mật `.env` đã được thiết lập đầy đủ các thông tin quan trọng (mật khẩu `sa`, thông tin kết nối MinIO,...).

---

## 2. Các bước khôi phục hệ thống

### Bước 1: Khởi động các dịch vụ Docker
Mở Terminal tại thư mục gốc của dự án và chạy lệnh sau để khởi động toàn bộ các container lên môi trường chạy:
```bash
docker compose up -d
```

### Bước 2: Khôi phục cơ sở dữ liệu SQL Server (ConnextDb)
Copy file backup từ thư mục lưu trữ riêng tư vào bên trong container SQL Server (thay `/opt/connext-backups` bằng đường dẫn thư mục riêng của bạn):
```bash
docker cp /opt/connext-backups/connext_backup.bak connext-sqlserver:/var/opt/mssql/backup/connext_backup.bak
```

Thực hiện lệnh khôi phục (Restore) thông qua `sqlcmd`:
```bash
docker exec -it connext-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "MẬT_KHẨU_SA_CỦA_BẠN" -C -Q "RESTORE DATABASE [ConnextDb] FROM DISK = N'/var/opt/mssql/backup/connext_backup.bak' WITH REPLACE;"
```
*(Lưu ý: Thay thế `"MẬT_KHẨU_SA_CỦA_BẠN"` bằng mật khẩu thực tế được cấu hình trong file `.env` của bạn).*

### Bước 3: Khôi phục dữ liệu MinIO (File Storage)
Copy file nén backup MinIO từ thư mục lưu trữ riêng tư vào trong container MinIO:
```bash
docker cp /opt/connext-backups/minio_backup.tar.gz connext-minio:/tmp/minio_backup.tar.gz
```

Giải nén dữ liệu trực tiếp vào phân vùng lưu trữ của container:
```bash
docker exec -it connext-minio tar -xzf /tmp/minio_backup.tar.gz -C /
```

Khởi động lại container MinIO để hệ thống cập nhật lại dữ liệu:
```bash
docker restart connext-minio
```

---

## 3. Kiểm tra hoàn tất
Sau khi thực hiện xong các bước trên:
- Kiểm tra trạng thái các container đang chạy bằng lệnh: `docker ps`
- Truy cập vào ứng dụng để kiểm tra tính toàn vẹn của dữ liệu (dữ liệu người dùng trên SQL Server và hình ảnh/tài liệu trên MinIO).