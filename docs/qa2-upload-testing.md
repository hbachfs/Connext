# QA2 - Upload File Testing

## 1. Muc dich

Kiem thu API Upload, Download, Media,
kiem tra dinh dang, dung luong va phan quyen.

Refs: #32, #50

## 2. Dieu kien kiem thu

- Backend va MinIO dang hoat dong.
- Su dung moi truong Local.
- Postman Working Directory la thu muc goc Connext.
- baseUrl tro den http://localhost:5232.
- token la JWT cua thanh vien A.
- convDToken la JWT cua nguoi ngoai hoi thoai D.
- convGroupId la ID nhom gom A, B2 va C.
- uploadFileId duoc luu sau TC_UPLOAD_01.

## 3. Tao file kiem thu

Mo PowerShell tai thu muc goc Connext va chay:

```powershell
"QA2 - Kiem thu upload file hop le cho Connext" | Set-Content ".\qa2-upload-test.txt" -Encoding UTF8

Copy-Item ".\qa2-upload-test.txt" ".\qa2-upload-invalid.exe"

Copy-Item ".\qa2-upload-test.txt" ".\qa2-upload-fake.png"

[System.IO.File]::WriteAllBytes("$PWD\qa2-upload-empty.txt", [byte[]]@())

$file = [System.IO.File]::Create("$PWD\qa2-upload-oversize.txt")
$file.SetLength(104857601)
$file.Close()
```

File EXE chi la file van ban doi duoi,
khong chua ma doc.

Khong commit cac file du lieu kiem thu,
Environment Local hoac thong tin xac thuc.

## 4. Ket qua kiem thu

| Test case | Noi dung | HTTP | Ket qua |
|---|---|---|---|
| TC_UPLOAD_01 | Upload TXT hop le | 201 | PASS |
| TC_UPLOAD_02 | Sai duoi EXE | 400 | PASS |
| TC_UPLOAD_03 | Sai chu ky | 400 | PASS |
| TC_UPLOAD_04 | File rong | 400 | PASS |
| TC_UPLOAD_05 | Qua dung luong | 400 | PASS |
| TC_UPLOAD_06 | Nguoi ngoai upload | 400 | PASS |
| TC_UPLOAD_07 | Thanh vien xem kho File | 200 | PASS |
| TC_UPLOAD_08 | Nguoi ngoai xem kho File | 404 | PASS |
| TC_UPLOAD_09 | Thanh vien download | 200 | PASS |
| TC_UPLOAD_10 | Nguoi ngoai download | 404 | PASS |
| TC_UPLOAD_11 | File khong ton tai | 404 | PASS |
| TC_UPLOAD_12 | Upload khong co JWT | 401 | PASS |

## 5. Tong ket

- Ngay kiem thu: 26/11/2026.
- Test case: 12/12 PASS.
- Assertions: 26/26 PASS.
- Moi truong: Local.
- Chay tung request bang Postman.
- Chua chay toan bo collection bang Newman.
- Chua xac minh tren Azure Production.

## 6. Luu y

Chay TC_UPLOAD_01 truoc TC_UPLOAD_09
va TC_UPLOAD_10 de co uploadFileId hop le.

Chi su dung file dung luong lon tren Local.
Khong commit mat khau, JWT va User Secrets.
