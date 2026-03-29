# Hệ thống quản lý sự kiện & học tập trực tuyến (DACSWEBSK)

Web **ASP.NET Core 8** — quản lý sự kiện, video & tiến độ, chứng chỉ, thông báo, đổi quà; tích hợp **ASP.NET Identity**, **EF Core**, **SQL Server**, email SMTP, hosted services (tác vụ nền), chatbot (Bot Framework) và tích hợp AI tùy cấu hình.

**Vai trò:** đồ án / dự án học tập — Backend & full-stack MVC.

## Công nghệ

- ASP.NET Core MVC, Razor, Entity Framework Core 9  
- SQL Server  
- ASP.NET Identity (đăng ký, đăng nhập, phân quyền)  
- Repository pattern, Dependency Injection  
- SMTP (gửi mail), background services (`IHostedService`)  
- Tùy chọn: Microsoft Bot Framework, OpenAI API, Hugging Face, script Python (transcript)

## Cấu trúc thư mục

| Thư mục | Nội dung |
|---------|----------|
| `Controllers/` | Controller MVC |
| `Models/` | Entity / ViewModel |
| `Views/` | Razor views |
| `Services/` | Nghiệp vụ, email, chứng chỉ, AI, YouTube… |
| `Repositories/` | Truy cập dữ liệu (interface + EF) |
| `Areas/` | Admin, Identity |
| `Migrations/` | Migration EF Core |
| `wwwroot/` | Static files, upload |

## Yêu cầu

- [.NET 8 SDK](https://dotnet.microsoft.com/download)  
- SQL Server (LocalDB, Express hoặc bản đầy đủ)  
- Visual Studio 2022 hoặc VS Code + C# Dev Kit (khuyến nghị)  
- Python (chỉ khi dùng tính năng transcript — chỉnh đường dẫn trong cấu hình)

## Cài đặt & chạy

1. **Clone repo** và mở thư mục `DACSWEBSK` (project `.csproj` nằm ở đây).

2. **Tạo file cấu hình local** (không commit — đã có trong `.gitignore`):

   ```bash
   copy appsettings.example.json appsettings.json
   ```

   Chỉnh trong `appsettings.json`:
   - `ConnectionStrings:DefaultConnection` — instance SQL Server và tên database của bạn  
   - `EmailSettings` — SMTP (ví dụ Gmail App Password)  
   - `OpenAI` / `HuggingFace` — chỉ khi bật tính năng tương ứng  
   - `Python:Path` và `Python:Scripts:Transcript` — đường dẫn thật trên máy bạn nếu dùng transcript  

3. **Áp database** (tại thư mục chứa `.csproj`):

   ```bash
   dotnet ef database update
   ```

   *(Cần cài EF tools nếu chưa có: `dotnet tool install --global dotnet-ef`.)*

4. **Chạy:**

   ```bash
   dotnet run
   ```

   Xem URL HTTPS/HTTP hiển thị trên console (thường `https://localhost:7xxx`).

## Lưu ý bảo mật

- **Không** đẩy `appsettings.json` / `appsettings.Development.json` chứa API key hoặc mật khẩu lên Git. Repo dùng `appsettings.example.json` làm mẫu.  
- Với production, nên dùng [User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) (dev) hoặc biến môi trường / Key Vault.

## Tính năng chính (tóm tắt)

1. Quản lý sự kiện, người tham gia, địa điểm  
2. Video & theo dõi tiến độ xem  
3. Chứng chỉ (tạo / gửi mail), thông báo  
4. Bài tập / minh chứng, đổi quà  
5. Chatbot & tích hợp AI (theo cấu hình)  
6. Khu vực Admin & Identity

## Tác giả

Trần Vũ Điền Quân — HUTECH, Công nghệ phần mềm.

## License

Dự án phục vụ học tập; sử dụng lại vui lòng ghi nguồn.

---

<details>
<summary>Kế hoạch thực hiện (10 tuần) — tham khảo</summary>

- Tuần 1: Phân tích, thiết kế DB & kiến trúc  
- Tuần 2: ASP.NET Core, EF Core, Identity  
- Tuần 3: Module sự kiện (CRUD, lịch, địa điểm, attendee)  
- Tuần 4: Video, tiến độ, YouTube / transcript  
- Tuần 5: Chứng chỉ, email  
- Tuần 6: Tương tác, thông báo  
- Tuần 7: AI & Bot  
- Tuần 8: Phần thưởng / đổi quà  
- Tuần 9: Tối ưu, bảo mật  
- Tuần 10: Kiểm thử, triển khai  

</details>
