using DACSWEBSK.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DACSWEBSK.Services.Jobs
{
    public static class EventStatusUpdateJob
    {
        public static async Task RunAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("EventStatusUpdateJob");
            var currentTime = DateTime.Now;

            logger.LogInformation("Checking event status at {Time}", currentTime);

            var completedEvents = await dbContext.Events
                .Include(e => e.Attendees)
                .Where(e => e.EndTime < currentTime &&
                            (e.Status == "Upcoming" || e.Status == "In Progress"))
                .ToListAsync(cancellationToken);

            foreach (var evt in completedEvents)
            {
                evt.Status = "Completed";
                logger.LogInformation("Marked event '{Title}' as Completed", evt.Title);

                foreach (var attendee in evt.Attendees)
                {
                    try
                    {
                        await emailService.SendEventEndedEmailAsync(
                            attendee.Email,
                            attendee.FullName,
                            evt.Title);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to send ended email to {Email}", attendee.Email);
                    }
                }
            }

            var inProgressEvents = await dbContext.Events
                .Where(e => e.StartTime <= currentTime &&
                            e.EndTime > currentTime &&
                            e.Status == "Upcoming")
                .ToListAsync(cancellationToken);

            foreach (var evt in inProgressEvents)
            {
                evt.Status = "In Progress";
                logger.LogInformation("Marked event '{Title}' as In Progress", evt.Title);
            }

            if (completedEvents.Count > 0 || inProgressEvents.Count > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation(
                    "Updated {Count} event(s)",
                    completedEvents.Count + inProgressEvents.Count);
            }
            else
            {
                logger.LogInformation("No events required status updates");
            }
        }
    }
}
