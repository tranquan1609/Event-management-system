# DACSWEBSK on AWS — Kiến trúc, danh sách dịch vụ & hướng dẫn triển khai

**Hệ thống:** Event Organization & Management System (ASP.NET Core MVC)  
**Region:** `ap-southeast-1` (Singapore)  
**Database:** Amazon RDS for **SQL Server Express Edition**  
**Kiến trúc tham chiếu:** Diagram *AWS Architecture – Event Organization & Management System*

Làm **đúng thứ tự** — mỗi phần xong phải test trước khi sang phần tiếp theo.

---

## Mục lục nhanh

| Phần | Nội dung |
|------|----------|
| [1](#1-kiến-trúc-mạng-vpc) | Kiến trúc mạng VPC (diagram) |
| [2](#2-danh-sách-dịch-vụ-aws-đã-chốt) | Danh sách dịch vụ đã chốt |
| [3](#3-lộ-trình-triển-khai-theo-phase) | Lộ trình Phase 1 / 2 / 3 |
| [4](#4-luồng-dữ-liệu-bước-18) | Luồng dữ liệu bước 1–8 |
| [5](#5-hướng-dẫn-triển-khai-từng-phần) | Hướng dẫn triển khai từng phần |
| [6](#6-chuẩn-vẽ-diagram--lỗi-cần-tránh) | Chuẩn vẽ diagram |
| [7](#7-checklist-tiến-độ) | Checklist tiến độ |
| [8](#8-clean-up) | Clean-up |
| [9](#9-📸-danh-sách-screenshot-cho-báo-cáo-workshop) | **Danh sách screenshot** |

---

## 9. 📸 Danh sách screenshot cho báo cáo Workshop

Lưu ảnh vào folder: `ThucTapAWSWorklog/.../static/images/5-Workshop/` (hoặc `screenshots/` tạm trên máy).

**Quy tắc đặt tên:** `5.X.Y-mo-ta-ngan.png` (ví dụ `5.1.3-rds-available.png`)

| # | Khi nào chụp | Chụp gì | Tên file gợi ý | Phần |
|---|--------------|---------|----------------|------|
| 📸-01 | Region góc phải Console | `ap-southeast-1` | `5.0-region.png` | 0 |
| 📸-02 | IAM → Users → `dacswebsk-dev` created | User list + policies attached | `5.2.1-iam-user.png` | 0 |
| 📸-03 | Terminal | `aws sts get-caller-identity` kết quả `user/dacswebsk-dev` | `5.2.2-aws-cli.png` | 0 |
| 📸-04 | RDS Create database | Engine SQL Server Express + Free tier | `5.3.1-rds-create-engine.png` | 1 |
| 📸-05 | RDS Create database | Connectivity: Public Yes + SG `dacswebsk-rds-sg` | `5.3.1-rds-connectivity.png` | 1 |
| 📸-06 | RDS Console | Status **Available** + endpoint | `5.3.2-rds-available.png` | 1 |
| 📸-07 | EC2 → Security Groups | Inbound 1433 / My IP | `5.3.3-rds-sg.png` | 1 |
| 📸-08 | Terminal | `dotnet ef database update` thành công | `5.3.4-ef-migrate.png` | 1 |
| 📸-09 | RDS Monitoring | DatabaseConnections > 0 | `5.3.5-rds-connections.png` | 1 |
| 📸-10 | App browser | Đăng ký user / tạo sự kiện thành công | `5.3.6-app-rds-test.png` | 1 |
| 📸-11 | S3 Console | 4 buckets đã tạo | `5.4.1-s3-buckets.png` | 2 |
| 📸-12 | S3 bucket → Permissions | Block all public access = On | `5.4.2-s3-block-public.png` | 2 |
| 📸-13 | S3 bucket → Objects | `events/` có file sau upload | `5.4.3-s3-upload-object.png` | 2 |
| 📸-14 | App browser | Trang sự kiện hiển thị ảnh từ S3 | `5.4.4-app-s3-image.png` | 2 |
| 📸-15 | Terminal | `aws s3 ls s3://dacswebsk-images-...` | `5.4.5-s3-cli-ls.png` | 2 |
| 📸-16 | SES Verified identities | Email status **Verified** | `5.5.1-ses-verified.png` | 4 |
| 📸-17 | Email inbox | Mail xác nhận đăng ký / chứng chỉ | `5.5.2-ses-email-received.png` | 4 |
| 📸-18 | VPC Console | VPC + public/private subnet (Phase 5) | `5.6.1-vpc.png` | 5 |
| 📸-19 | EC2 Instances | Instance running + private subnet | `5.6.2-ec2.png` | 5 |
| 📸-20 | Browser | App chạy trên Elastic IP / ALB | `5.6.3-ec2-app.png` | 5 |
| 📸-21 | Lambda Console | 3–4 functions deployed | `5.7.1-lambda.png` | 6 |
| 📸-22 | EventBridge Rules | Cron rules enabled | `5.7.2-eventbridge.png` | 6 |
| 📸-23 | CloudWatch Alarms | Alarm state OK/ALARM | `5.8.1-cloudwatch-alarm.png` | 8 |

> **Bắt buộc cho rubric:** mỗi phần lab cần ít nhất **3 screenshot** (tạo resource → cấu hình → test kết quả).

---

## 1. Kiến trúc mạng (VPC)

### 1.1. Sơ đồ mục tiêu (diagram đã chốt)

```
                        ┌── Amazon VPC (10.0.0.0/16) ──────────────────────────────┐
                        │                                                           │
[User]──HTTPS──►        │  Public Subnet (10.0.1.0/24)                              │
 Route53 ──► WAF ──► ALB│       │                                                   │
 (Phase 3)              │       ▼                                                   │
                        │  Private Subnet (10.0.2.0/24)                             │
                        │       EC2 (ASP.NET Core MVC + ASP.NET Identity)           │
                        │         │◄──────────────────────────► RDS SQL Server       │
                        │         │◄──────────────────────────► S3 (Block Public)    │
                        │       NAT Gateway ──► Internet (outbound: SES, Bedrock…)  │
                        └───────────────────────────────────────────────────────────┘

[EventBridge] ──cron──► Lambda (VPC private) ──► RDS / S3 / SES
[User] ──POST /chat──► API Gateway (Phase 2) ──► Lambda Chatbot ──► RDS (+ Bedrock optional)

[CloudWatch] ◄── EC2, Lambda, RDS, WAF
[IAM · Secrets Manager · KMS · CloudTrail · Config · Backup · Shield]
```

### 1.2. Thành phần VPC

| Thành phần | CIDR / vị trí | Ghi chú |
|------------|---------------|---------|
| **VPC** | `10.0.0.0/16` | Mạng riêng toàn project |
| **Public Subnet** | `10.0.1.0/24` | Chỉ **ALB** (+ NAT Gateway) |
| **Private Subnet** | `10.0.2.0/24` | **EC2** + **RDS** |
| **NAT Gateway** | Public subnet | EC2/Lambda ra internet (HTTPS outbound) |
| **Internet Gateway** | Gắn VPC | Cho ALB nhận traffic từ internet |

### 1.3. Cửa vào internet (chỉ 2 điểm)

| Cửa vào | Phase | Mô tả |
|---------|-------|-------|
| **ALB** (sau WAF) | Phase 3 | Web MVC chính |
| **API Gateway** | Phase 2 | Chatbot `POST /chat` |

**Không** expose RDS ra internet. **S3** bật Block Public Access — truy cập qua IAM role / pre-signed URL.

### 1.4. Phase triển khai thực tế vs diagram

| Giai đoạn | Truy cập web | Database | Ghi chú |
|-----------|--------------|----------|---------|
| **Lab Phase 1** | `dotnet run` local | RDS **public** + SG My IP | Nhanh, dễ test |
| **Lab Phase 2** | EC2 + **Elastic IP** | RDS private hoặc SG EC2 | Chưa cần ALB/WAF |
| **Báo cáo Phase 3** | Route53 → WAF → ALB → EC2 private | RDS private subnet | Khớp diagram đầy đủ |

---

## 2. Danh sách dịch vụ AWS đã chốt

### 2.1. Dịch vụ triển khai chính

| # | Dịch vụ | Vai trò | Code / file |
|---|---------|---------|-------------|
| 1 | **Amazon VPC** | Public/private subnet, NAT, IGW | Hạ tầng |
| 2 | **Amazon EC2** | Host MVC + **ASP.NET Identity** (1 instance) | Toàn project |
| 3 | **Amazon RDS (SQL Server)** | Database | `ApplicationDbContext`, `Migrations/` |
| 4 | **Amazon S3** | File upload (Block Public Access) | `IFileStorageService`, `S3FileStorageService` |
| 5 | **Amazon SES** | Email | `EmailService.cs`, `SmtpEmailSender.cs` |
| 6 | **AWS Lambda** | 4 functions (VPC private) | Xem bảng 2.2 |
| 7 | **Amazon EventBridge** | Cron trigger Lambda | Thay `IHostedService` |
| 8 | **Amazon API Gateway** | Chatbot `POST /chat` (Phase 2) | `_ChatBot.cshtml` |
| 9 | **Amazon CloudWatch** | Logs, metrics, alarms | EC2 agent + Lambda |

### 2.2. Bốn Lambda function

| Lambda | Trigger | Làm gì | Code nguồn |
|--------|---------|--------|------------|
| `dacsweb-event-status` | EventBridge `rate(1 minute)` | Cập nhật trạng thái Event | `EventStatusUpdateService.cs` |
| `dacsweb-auto-certificate` | EventBridge `rate(1 minute)` | Cấp chứng chỉ → S3 → SES | `AutoCertificateService.cs`, `CertificateService.cs` |
| `dacsweb-notification` | EventBridge `rate(1 minute)` | Email khi sự kiện kết thúc | `EventEndedEmailService.cs` |
| `dacsweb-chatbot` | API Gateway `POST /chat` | Chatbot, query RDS | `DacsBot.cs` |

> **Lưu ý:** Lambda đặt trong **VPC private subnet** để kết nối RDS private.

### 2.3. Bảo mật & quản trị

| Dịch vụ | Vai trò |
|---------|---------|
| **AWS IAM** | User `dacswebsk-dev` (dev CLI); Role cho EC2/Lambda |
| **AWS Secrets Manager** | Connection string, API keys |
| **AWS KMS** | Mã hóa Secrets, S3, RDS |

### 2.4. Phase 3 — Diagram / production (optional)

| Dịch vụ | Vai trò |
|---------|---------|
| **Amazon Route 53** | DNS |
| **AWS WAF** | Trước ALB — **một cửa vào web** |
| **Application Load Balancer** | Trỏ vào EC2 private subnet |
| **Amazon Cognito** | Optional / future (mobile, API) |
| **Amazon Bedrock** | Optional — thay OpenAI/HuggingFace |

### 2.5. Shared Services (báo cáo)

CloudTrail · AWS Config · AWS Backup · AWS Shield Standard

---

## 3. Lộ trình triển khai theo Phase

### Phase 1 — Nền tảng (bắt đầu từ đây)

| Phần | Nội dung | Dịch vụ | Sửa code |
|------|----------|---------|----------|
| **0** | AWS CLI + IAM User | IAM | Không |
| **1** | Database | RDS SQL Server | Connection string |
| **2** | S3 — ảnh sự kiện | S3 | Đã tích hợp `AdminEventController` |
| **3** | S3 — module còn lại | S3 | Certificate, Video, Gift, Assignment, Evidence |
| **4** | Email | SES | Config SMTP |

### Phase 2 — Cloud & serverless

| Phần | Nội dung | Dịch vụ | Sửa code |
|------|----------|---------|----------|
| **5** | VPC + EC2 deploy | VPC, EC2, NAT, IAM Role, Secrets Manager | `AppSettings:BaseUrl` |
| **6** | Job nền | Lambda + EventBridge (VPC) | Tách BackgroundService |
| **7** | Chatbot | API Gateway + Lambda | `_ChatBot.cshtml` |
| **8** | Monitoring | CloudWatch | Agent + alarms |

### Phase 3 — Khớp diagram báo cáo (optional)

| Phần | Nội dung | Dịch vụ |
|------|----------|---------|
| **9** | DNS + WAF + ALB | Route 53, WAF, ALB |
| **10** | AI AWS | Bedrock |
| **11** | Auth mở rộng | Cognito |
| **12** | Audit | CloudTrail, Config, Backup, KMS |

---

## 4. Luồng dữ liệu (bước 1–8)

| Bước | Luồng | Loại | Mô tả |
|------|-------|------|-------|
| **1** | User → Route 53 | Sync HTTPS | DNS resolve (Phase 3) |
| **2** | Route 53 → WAF | Sync HTTPS | Lọc request độc hại |
| **3** | WAF → ALB | Sync HTTPS | Phân tải |
| **4** | ALB → EC2 → User | Sync HTTPS | Request + **response HTML/JSON ngược lại** |
| **4a** | EC2: Identity middleware | Sync | **Auth trước** controller — không vào Admin khi chưa login |
| **5** | EC2 ◄──► RDS | Sync | Đọc/ghi SQL Server (TLS) |
| **6** | EC2 ◄──► S3 | Sync | Upload/download (IAM role, bucket private) |
| **7** | EventBridge → Lambda → SES → User | Async | Email chứng chỉ / thông báo |
| **8** | API GW → Lambda Chatbot → Bedrock (optional) → User | Sync | Phase 2 — chatbot JSON response |

**Legend diagram:**
- **Nét liền** = sync HTTPS
- **Nét đứt** = async (EventBridge, job nền)
- **Nét chấm** = optional / Phase 2–3 (Cognito, Bedrock, Route53/WAF/ALB)

**Ghi chú bảo mật (ghi trên diagram):**
- Chỉ **ALB** và **API Gateway** là entry point internet
- RDS trong **private subnet**, không public IP
- S3 **Block Public Access**

---

## 5. Hướng dẫn triển khai từng phần

---

### Phần 0 — Chuẩn bị (~30 phút)

#### Bước 0.1 — Cài AWS CLI

```powershell
winget install Amazon.AWSCLI
aws --version
```

#### Bước 0.2 — Tạo IAM User (không dùng Root)

1. Console → Region: **ap-southeast-1**
2. **IAM** → **Users** → **Create user** → `dacswebsk-dev`
3. **Không** bật console access (chỉ CLI)
4. Attach policies:
   - `AmazonS3FullAccess`
   - `AmazonRDSFullAccess`
   - `AmazonSESFullAccess`
   - `AmazonEC2FullAccess`
   - `AWSLambda_FullAccess`
   - `AmazonVPCFullAccess`
   - `CloudWatchFullAccess`
   - `AmazonAPIGatewayAdministrator`
   - `SecretsManagerReadWrite`
5. **Create access key** → CLI → lưu key

```powershell
aws configure
# AWS Access Key ID: ...
# AWS Secret Access Key: ...
# Default region name: ap-southeast-1
# Default output format: json

aws sts get-caller-identity
```

Kết quả đúng: `"Arn": "...:user/dacswebsk-dev"` (không phải `:root`).

> 📸 **Screenshot:** 📸-01 (region), 📸-02 (IAM user), 📸-03 (`aws sts get-caller-identity`)

#### Bước 0.3 — File cấu hình app

```powershell
cd "c:\New folder\UpdatehethongdesudungAWS\DACNWEBSK\DACSWEBSK"
copy appsettings.example.json appsettings.json
```

Giữ `"Storage": { "Provider": "Local" }` cho đến Phần 2.

#### Bước 0.4 — Chạy thử local

```powershell
dotnet build
dotnet run
```

- [ ] Trang chủ load được (`https://localhost:7080`)
- [ ] Đăng nhập Admin được

---

### Phần 1 — Amazon RDS SQL Server (~45 phút)

> **Lab Phase 1:** RDS public + Security Group My IP — để dev từ máy local.  
> **Sau Phần 5:** chuyển RDS sang private subnet, chỉ cho phép EC2/Lambda SG.

#### Bước 1.1 — Tạo database

1. Console → **RDS** → **Create database**
2. **Engine:** Microsoft SQL Server Express Edition
3. **Template:** Free tier
4. **DB instance identifier:** `dacswebsk-db`
5. **Master username:** `admin` | **password:** (ghi lại)
6. **Instance:** `db.t3.micro` | **Storage:** 20 GB gp2
7. **Connectivity:**
   - VPC: default (hoặc VPC sẽ tạo ở Phần 5)
   - **Public access: Yes** *(chỉ lab Phase 1)*
   - VPC security group: tạo mới `dacswebsk-rds-sg`
8. **Create database** → đợi status **Available**

#### Bước 1.2 — Security Group RDS

EC2 Console → **Security Groups** → `dacswebsk-rds-sg`:

| Type | Port | Source |
|------|------|--------|
| MSSQL | 1433 | **My IP** |

#### Bước 1.3 — Lấy endpoint

RDS Console → `dacswebsk-db` → **Connectivity & security** → copy endpoint.

Hoặc CLI:

```powershell
aws rds describe-db-instances --db-instance-identifier dacswebsk-db --query "DBInstances[0].Endpoint.Address" --region ap-southeast-1
```

#### Bước 1.4 — Cập nhật `appsettings.json`

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=ENDPOINT,1433;Database=DACS;User Id=admin;Password=YOUR_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=true;Encrypt=True"
}
```

#### Bước 1.5 — Migrate database

```powershell
cd "c:\New folder\UpdatehethongdesudungAWS\DACNWEBSK\DACSWEBSK"
dotnet ef database update
dotnet run
```

#### Bước 1.6 — Test

- [ ] `dotnet run` — không lỗi kết nối
- [ ] Đăng ký user mới / tạo sự kiện
- [ ] RDS Console → **Monitoring** → DatabaseConnections > 0

**Lỗi thường gặp:**

| Triệu chứng | Cách xử lý |
|-------------|------------|
| Timeout port 1433 | Kiểm tra SG My IP, Public access = Yes |
| Login failed | Kiểm tra username/password |
| Cannot open database | Chạy lại `dotnet ef database update` |

> 📸 **Screenshot:** 📸-04 → 📸-10 (xem [mục 9](#9-📸-danh-sách-screenshot-cho-báo-cáo-workshop))

**Section summary (ghi vào báo cáo):** Đã migrate database DACSWEBSK lên Amazon RDS SQL Server; app kết nối từ local qua endpoint port 1433.

---

### Phần 2 — Amazon S3 — ảnh sự kiện (~1 giờ)

Code hỗ trợ `Storage:Provider` = `Local` | `S3`.

#### Bước 2.1 — Tạo buckets

Thay `YOURNAME` bằng tên duy nhất (ví dụ `varihuynh`):

```powershell
aws s3 mb s3://dacswebsk-images-YOURNAME --region ap-southeast-1
aws s3 mb s3://dacswebsk-videos-YOURNAME --region ap-southeast-1
aws s3 mb s3://dacswebsk-certificates-YOURNAME --region ap-southeast-1
aws s3 mb s3://dacswebsk-uploads-YOURNAME --region ap-southeast-1
```

Mỗi bucket: **Block all public access** = Bật.

#### Bước 2.2 — Upload template chứng chỉ

```powershell
aws s3 cp "wwwroot\images\certificate-template.png" s3://dacswebsk-images-YOURNAME/templates/certificate-template.png
```

#### Bước 2.3 — Cập nhật `appsettings.json`

```json
"Storage": { "Provider": "S3" },
"AWS": { "Region": "ap-southeast-1" },
"S3": {
  "ImagesBucket": "dacswebsk-images-YOURNAME",
  "VideosBucket": "dacswebsk-videos-YOURNAME",
  "CertificatesBucket": "dacswebsk-certificates-YOURNAME",
  "UploadsBucket": "dacswebsk-uploads-YOURNAME",
  "PresignedUrlExpiryMinutes": 60
}
```

Credentials: dùng `aws configure` — **không** ghi access key vào appsettings.

#### Bước 2.4 — Test

1. `dotnet run`
2. Admin → Tạo sự kiện → upload ảnh
3. S3 Console → bucket `images` → folder `events/` có file
4. Ảnh hiển thị trên web

**Rollback nếu lỗi:** đổi `"Provider": "Local"`.

> 📸 **Screenshot:** 📸-11 → 📸-15

**Section summary:** Ảnh sự kiện upload qua app được lưu Amazon S3 (bucket private); hiển thị web qua pre-signed URL.

---

### Phần 3 — S3 module còn lại

| Module | File code | Bucket |
|--------|-----------|--------|
| Chứng chỉ | `CertificateService.cs` | `CertificatesBucket` |
| Video | `VideoController.cs` | `VideosBucket` |
| Quà | `AdminGiftController.cs` | `ImagesBucket` |
| Bài tập | `AssignmentController.cs` | `UploadsBucket` |
| Minh chứng | `EvidenceController.cs` | `UploadsBucket` |

---

### Phần 4 — Amazon SES

1. **SES** → **Verified identities** → verify email gửi
2. **SES** → **SMTP settings** → Create SMTP credentials
3. Cập nhật `appsettings.json`:

```json
"EmailSettings": {
  "SmtpHost": "email-smtp.ap-southeast-1.amazonaws.com",
  "SmtpPort": "587",
  "SmtpUsername": "SMTP_USER_FROM_SES",
  "SmtpPassword": "SMTP_PASS_FROM_SES"
}
```

**Sandbox:** verify cả email người nhận test, hoặc xin production access.

**Test:** Đăng ký user → nhận email mã xác nhận.

> 📸 **Screenshot:** 📸-16 (SES verified), 📸-17 (email nhận được)

**Section summary:** Email xác nhận và thông báo gửi qua Amazon SES thay Gmail SMTP.

---

### Phần 5 — VPC + Amazon EC2

#### Bước 5.1 — Tạo VPC (khớp diagram)

| Resource | Giá trị |
|----------|---------|
| VPC CIDR | `10.0.0.0/16` |
| Public Subnet | `10.0.1.0/24` — AZ `ap-southeast-1a` |
| Private Subnet | `10.0.2.0/24` — AZ `ap-southeast-1a` |
| Internet Gateway | Gắn VPC |
| NAT Gateway | Trong public subnet |
| Route table public | `0.0.0.0/0` → IGW |
| Route table private | `0.0.0.0/0` → NAT |

#### Bước 5.2 — Security Groups

| SG | Inbound |
|----|---------|
| `dacswebsk-alb-sg` | 80, 443 từ `0.0.0.0/0` |
| `dacswebsk-ec2-sg` | 80, 443 từ `dacswebsk-alb-sg` *(Phase 3)* hoặc 80 từ My IP *(Phase 2 EIP)* |
| `dacswebsk-rds-sg` | 1433 từ `dacswebsk-ec2-sg` + Lambda SG |

#### Bước 5.3 — IAM Role `DacsWebEc2Role`

Quyền tối thiểu: S3 (bucket cụ thể), SES SendEmail, Secrets Manager GetSecretValue, CloudWatch Logs.

#### Bước 5.4 — Launch EC2

| Mục | Giá trị |
|-----|---------|
| AMI | Windows Server 2022 hoặc Amazon Linux 2023 |
| Type | `t3.small` |
| Subnet | **Private** `10.0.2.0/24` |
| IAM Role | `DacsWebEc2Role` |
| SG | `dacswebsk-ec2-sg` |

**Phase 2 nhanh:** EC2 public subnet + Elastic IP (chưa ALB).  
**Phase 3 báo cáo:** EC2 private + ALB public.

#### Bước 5.5 — Deploy app

```powershell
dotnet publish -c Release -o ./publish
# Copy publish/ lên EC2, chạy:
dotnet DACSWEBSK.dll --urls "http://0.0.0.0:80"
```

#### Bước 5.6 — RDS chuyển private

1. RDS → Modify → **Public access: No**
2. RDS SG: chỉ cho phép `dacswebsk-ec2-sg` + Lambda SG
3. Cập nhật `AppSettings:BaseUrl` = URL public (EIP hoặc ALB domain)

---

### Phần 6 — Lambda + EventBridge (VPC)

1. Tạo project `DACSWEBSK.Lambda` (.NET 8)
2. Deploy 3 functions trong **VPC private subnet**
3. Gán Lambda SG — inbound RDS cho phép từ Lambda SG
4. EventBridge rules:

```powershell
aws events put-rule --name dacsweb-event-status-cron --schedule-expression "rate(1 minute)" --region ap-southeast-1
```

5. Comment trong `Program.cs`:

```csharp
// builder.Services.AddHostedService<EventStatusUpdateService>();
// builder.Services.AddHostedService<AutoCertificateService>();
// builder.Services.AddHostedService<EventEndedEmailService>();
```

**Test:** Event kết thúc → status đổi → chứng chỉ S3 → email SES.

---

### Phần 7 — API Gateway + Chatbot (Phase 2)

1. HTTP API → `POST /chat` → Lambda `dacsweb-chatbot`
2. Cập nhật `Views/Shared/_ChatBot.cshtml` → API Gateway URL
3. (Optional) WAF regional cho API GW

```powershell
curl -X POST "https://API_ID.execute-api.ap-southeast-1.amazonaws.com/chat" `
  -H "Content-Type: application/json" `
  -d "{\"text\":\"sự kiện sắp tới\"}"
```

---

### Phần 8 — CloudWatch

| Alarm | Metric | Ngưỡng |
|-------|--------|--------|
| `dacsweb-ec2-high-cpu` | EC2 CPUUtilization | > 80% / 5 phút |
| `dacsweb-lambda-errors` | Lambda Errors | ≥ 1 |
| `dacsweb-rds-storage` | RDS FreeStorageSpace | < 2 GB |
| `dacsweb-alb-5xx` | ALB HTTPCode_Target_5XX | ≥ 1 *(Phase 3)* |

Log groups: `/aws/lambda/dacsweb-*`, EC2 application logs.

**(Optional)** CloudWatch Alarm → SNS → email Admin.

---

### Phần 9–12 — Phase 3 (diagram đầy đủ)

Chi tiết khi hoàn thành Phase 1–2. Tham chiếu diagram: Route53 → WAF → ALB → EC2 private.

---

## 6. Chuẩn vẽ diagram & lỗi cần tránh

### 6.1. Checklist diagram (đã chốt)

- [x] WAF trước ALB (không chỉ một nhánh API)
- [x] 1 EC2 duy nhất: MVC + Identity trong private subnet
- [x] RDS private, không public
- [x] S3 Block Public Access
- [x] NAT Gateway cho outbound
- [x] Cognito = optional / future (nét chấm)
- [x] API Gateway = Phase 2 chatbot
- [x] Legend: liền / đứt / chấm
- [x] Ghi response ngược HTTPS (bước 4)
- [x] Lambda trong VPC private

### 6.2. Lỗi thường bị chê (tránh)

| Lỗi | Cách tránh |
|-----|------------|
| WAF chỉ trên API, không trên web | WAF → ALB cho toàn bộ traffic web |
| Icon DB chồng nhau | Mỗi service một ô riêng |
| Data ra internet không bảo vệ | VPC private + HTTPS + S3 private |
| Auth sau khi vào compute | Identity middleware **trước** controller |
| 2 EC2 tách Identity/App | **1 EC2** monolith |
| Chỉ vẽ data in, không data out | Ghi response ngược hoặc legend |

---

## 7. Checklist tiến độ

### Phase 1 — Đang làm
- [ ] **0** — `aws sts get-caller-identity` OK (`user/dacswebsk-dev`)
- [ ] **1** — RDS SQL Server + `dotnet ef database update`
- [ ] **2** — S3 buckets + upload ảnh sự kiện
- [ ] **3** — S3 toàn bộ module
- [ ] **4** — SES gửi email

### Phase 2
- [ ] **5** — VPC + EC2 deploy
- [ ] **6** — Lambda + EventBridge (VPC)
- [ ] **7** — API Gateway + Chatbot
- [ ] **8** — CloudWatch logs + alarms

### Phase 3 (báo cáo)
- [ ] **9** — Route 53 + WAF + ALB
- [ ] **10** — Bedrock
- [ ] **11** — Cognito
- [ ] **12** — CloudTrail, Config, Backup, KMS

---

## 8. Clean-up

```powershell
# EventBridge + Lambda
aws events remove-targets --rule RULE_NAME --ids 1 --region ap-southeast-1
aws events delete-rule --name RULE_NAME --region ap-southeast-1
aws lambda delete-function --function-name FUNCTION_NAME --region ap-southeast-1

# API Gateway
aws apigatewayv2 delete-api --api-id API_ID --region ap-southeast-1

# CloudWatch Alarms
aws cloudwatch delete-alarms --alarm-names ALARM_NAME --region ap-southeast-1

# S3
aws s3 rm s3://BUCKET_NAME --recursive
aws s3 rb s3://BUCKET_NAME

# RDS
aws rds delete-db-instance --db-instance-identifier dacswebsk-db --skip-final-snapshot --region ap-southeast-1

# EC2 + Elastic IP
aws ec2 terminate-instances --instance-ids INSTANCE_ID --region ap-southeast-1
aws ec2 release-address --allocation-id ALLOCATION_ID --region ap-southeast-1

# NAT Gateway (tốn phí nếu để qua đêm)
# Xóa NAT GW, IGW, VPC khi không dùng

# Secrets Manager
aws secretsmanager delete-secret --secret-id SECRET_NAME --force-delete-without-recovery --region ap-southeast-1
```

**Nhắc nhở chi phí:** Elastic IP không gắn instance · NAT Gateway · RDS running · EC2 running.

---

## Bước tiếp theo ngay bây giờ

1. Hoàn thành **Phần 0** nếu chưa xong (`aws sts get-caller-identity`)
2. Bắt đầu **Phần 1** — tạo RDS SQL Server theo mục [Phần 1](#phần-1--amazon-rds-sql-server-45-phút)
3. Sau khi RDS OK → **Phần 2** S3

---

*Bản cập nhật: khớp diagram VPC 10.0.0.0/16 · EC2 MVC+Identity · RDS private · WAF/ALB Phase 3.*
