using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using DACSWEBSK.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DACSWEBSK.Services
{
    public class EventStatusUpdateService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1); // Kiểm tra mỗi phút

        public EventStatusUpdateService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            Console.WriteLine("EventStatusUpdateService đã được khởi tạo");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("EventStatusUpdateService bắt đầu chạy");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try 
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                        var currentTime = DateTime.Now;

                        Console.WriteLine($"Đang kiểm tra status lúc: {currentTime}");

                        // Cập nhật status cho các sự kiện đã kết thúc (bao gồm cả những sự kiện đang In Progress)
                        var completedEvents = await dbContext.Events
                            .Include(e => e.Attendees) // Include attendees for email notifications
                            .Where(e => e.EndTime < currentTime && 
                                   (e.Status == "Upcoming" || e.Status == "In Progress"))
                            .ToListAsync(stoppingToken);

                        foreach (var evt in completedEvents)
                        {
                            // Cập nhật status
                            evt.Status = "Completed";
                            Console.WriteLine($"Cập nhật sự kiện '{evt.Title}' thành Completed");

                            // Gửi email cho tất cả người tham gia
                            foreach (var attendee in evt.Attendees)
                            {
                                try
                                {
                                    await emailService.SendEventEndedEmailAsync(
                                        attendee.Email,
                                        attendee.FullName,
                                        evt.Title
                                    );
                                    Console.WriteLine($"Đã gửi email thông báo kết thúc sự kiện đến {attendee.Email}");
                                }
                                catch (Exception emailEx)
                                {
                                    Console.WriteLine($"Lỗi khi gửi email đến {attendee.Email}: {emailEx.Message}");
                                }
                            }
                        }

                        // Cập nhật status cho các sự kiện đang diễn ra
                        var inProgressEvents = await dbContext.Events
                            .Where(e => e.StartTime <= currentTime && 
                                      e.EndTime > currentTime && 
                                      e.Status == "Upcoming")
                            .ToListAsync(stoppingToken);

                        foreach (var evt in inProgressEvents)
                        {
                            evt.Status = "In Progress";
                            Console.WriteLine($"Cập nhật sự kiện '{evt.Title}' thành In Progress");
                        }

                        if (completedEvents.Any() || inProgressEvents.Any())
                        {
                            await dbContext.SaveChangesAsync(stoppingToken);
                            Console.WriteLine($"Đã cập nhật {completedEvents.Count + inProgressEvents.Count} sự kiện");
                        }
                        else
                        {
                            Console.WriteLine("Không có sự kiện nào cần cập nhật");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi trong EventStatusUpdateService: {ex.Message}");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }
    }
} 