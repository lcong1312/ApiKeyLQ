# ApiKey License Store

Web quản lý và bán license/API key viết bằng ASP.NET MVC. Dự án hỗ trợ tạo key thủ công trong trang quản trị, bán key qua PayOS, kiểm tra key qua API, giới hạn request theo ngày, khóa/mở key, gia hạn, reset thiết bị và cấu hình nhiều sản phẩm bán hàng.

## Tính Năng Chính

- Trang bán hàng hiển thị nhiều sản phẩm.
- Mỗi sản phẩm có tên, mô tả, icon, link tải ứng dụng và nhiều gói giá theo số ngày.
- Thanh toán qua PayOS và tự sinh license key sau khi thanh toán thành công.
- Trang quản trị API key:
  - Tạo key theo số ngày hoặc ngày hết hạn.
  - Sửa thông tin key, owner, mô tả, giới hạn request/ngày, whitelist IP.
  - Bật/tắt key.
  - Gia hạn key.
  - Reset thiết bị đã liên kết.
  - Tuỳ chọn cho phép một key dùng nhiều thiết bị cùng lúc.
- API active key:
  - `POST /api/keys/activate`
  - `POST /api/keys/activate1`
- Dữ liệu lưu bằng JSON trong `App_Data`.

## Công Nghệ

- ASP.NET MVC 5
- .NET Framework
- Razor View Engine
- Newtonsoft.Json
- BCrypt.Net
- PayOS API
- FontAwesome

## Cấu Trúc Chính

- `Controllers/ApiController.cs`: API active key và quản lý key.
- `Controllers/AdminController.cs`: dashboard, danh sách key, cấu hình bán hàng.
- `Controllers/HomeController.cs`: trang bán hàng, tạo thanh toán, kiểm tra trạng thái PayOS.
- `Models/ApiKeyModel.cs`: model license/API key.
- `Models/StoreSettingsModel.cs`: model sản phẩm, gói bán hàng.
- `Helpers/JsonDbHelper.cs`: đọc/ghi dữ liệu JSON.
- `Helpers/OrderDbHelper.cs`: lưu đơn hàng PayOS.
- `Views/Admin/Keys.cshtml`: giao diện quản lý key.
- `Views/Admin/StoreSettings.cshtml`: giao diện cấu hình sản phẩm bán hàng.
- `Views/Home/Index.cshtml`: trang bán hàng.
- `App_Start/RouteConfig.cs`: route chính của website và API.

## Chạy Local

1. Mở project bằng Visual Studio.
2. Restore NuGet packages nếu Visual Studio chưa tự restore.
3. Kiểm tra cấu hình trong `Web.config`, đặc biệt các thông tin PayOS nếu dùng thanh toán thật.
4. Chạy bằng IIS Express.
5. Truy cập:

```text
/
/quantri
/Admin/Keys
/Admin/StoreSettings
```

## API Active Key

Các endpoint active chính:

```text
POST /api/keys/activate
POST /api/keys/activate1
```

Payload được mã hóa AES theo logic trong `AesCryptoHelper`. Sau khi giải mã, hệ thống đọc các trường:

```json
{
  "key": "license_key",
  "device_id": "device_id",
  "device_name": "device_name"
}
```

Nếu key không bật chế độ nhiều thiết bị, lần active đầu tiên sẽ liên kết `DeviceId`; các thiết bị khác dùng key đó sẽ bị từ chối. Nếu bật `Cho phép dùng nhiều thiết bị cùng lúc`, hệ thống bỏ qua kiểm tra `DeviceId`.

## Dữ Liệu Và Bảo Mật

Dữ liệu runtime nằm trong `App_Data`, ví dụ:

- `App_Data/db.json`
- `App_Data/orders.json`

Các file này có thể chứa admin account, license key, order và dữ liệu người dùng, nên đã được đưa vào `.gitignore`.

Không nên commit:

- `bin/`
- `obj/`
- `.vs/`
- `*.user`
- `App_Data/*.json`
- log/cache/temp files

## Ghi Chú

Project hiện dùng JSON file làm database nên phù hợp cho triển khai nhỏ hoặc quản trị đơn giản. Nếu lượng người dùng/request lớn, nên chuyển sang database thật như SQL Server để tránh tranh chấp ghi file và dễ backup/migration hơn.
