using DACSWEBSK.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using DACSWEBSK.Data;
using DACSWEBSK.Repositories.Interfaces;
using DACSWEBSK.Services; // Đã đúng namespace
using DACSWEBSK.Service.Bot;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using DACSWEBSK.Repositories.Implementations;
using DACSWEBSK.Services.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IEventRepository, EFEventRepository>();
builder.Services.AddScoped<IGiftRepository, EFGiftRepository>();
builder.Services.AddScoped<ILocationRepository, EFLocationRepository>();
builder.Services.AddScoped<INotificationRepository, EFNotificationRepository>();
builder.Services.AddScoped<IVideoRepository, EFVideoRepository>();
builder.Services.AddScoped<VideoProcessingService>();

// Add VideoProgressService
builder.Services.AddScoped<IVideoProgressService, VideoProgressService>();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddDefaultTokenProviders()
    .AddDefaultUI()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = $"/Identity/Account/Login";
    options.LogoutPath = $"/Identity/Account/Logout";
    options.AccessDeniedPath = $"/Identity/Account/AccessDenied";
});

// Đăng ký IEmailSender sử dụng SmtpEmailSender
builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, SmtpEmailSender>();

// Add Bot Framework services
builder.Services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();
builder.Services.AddTransient<IBot, DacsBot>(provider =>
    new DacsBot(
        provider.GetRequiredService<ApplicationDbContext>(),
        provider.GetRequiredService<DACSWEBSK.Services.OpenAIService>()
    )
);

// Add email service
builder.Services.AddScoped<IEmailService, EmailService>();

// Add OpenAI service
builder.Services.AddSingleton<DACSWEBSK.Services.OpenAIService>();

// Add Hugging Face service
builder.Services.AddSingleton<HuggingFaceService>();

// Add YouTubeTranscriptService
builder.Services.AddScoped<YoutubeDlService>();

// Background jobs chạy trên AWS Lambda + EventBridge (không chạy trùng trên EC2)
// builder.Services.AddHostedService<EventStatusUpdateService>();
// builder.Services.AddHostedService<AutoCertificateService>();
// builder.Services.AddHostedService<EventEndedEmailService>();

// Add certificate service
builder.Services.AddScoped<ICertificateService, CertificateService>();
builder.Services.AddScoped<CertificateTemplateService>();
builder.Services.AddHttpContextAccessor();

// File storage: Local (wwwroot) hoặc Amazon S3 — đổi Storage:Provider trong appsettings.json
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.Configure<S3StorageOptions>(builder.Configuration.GetSection(S3StorageOptions.SectionName));
var storageProvider = builder.Configuration[$"{StorageOptions.SectionName}:Provider"] ?? "Local";
if (storageProvider.Equals("S3", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
    builder.Services.AddAWSService<Amazon.S3.IAmazonS3>();
    builder.Services.AddScoped<IFileStorageService, S3FileStorageService>();
}
else
{
    builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
}

// Add YoutubeDlService
builder.Services.AddScoped<YoutubeDlService>();

// Configure SSL/TLS
System.Net.ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllerRoute(
        name: "areas",
        pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");
    endpoints.MapRazorPages();
});
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
