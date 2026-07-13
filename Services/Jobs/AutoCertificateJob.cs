using DACSWEBSK.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DACSWEBSK.Services.Jobs
{
    public static class AutoCertificateJob
    {
        public static async Task RunAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var certificateService = scope.ServiceProvider.GetRequiredService<ICertificateService>();
            var videoProgressService = scope.ServiceProvider.GetRequiredService<IVideoProgressService>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AutoCertificateJob");

            await ProcessRecentlyEndedEventsAsync(
                context, certificateService, videoProgressService, logger, cancellationToken);
            await RecheckPendingCertificatesAsync(
                context, certificateService, videoProgressService, logger, cancellationToken);
        }

        private static async Task ProcessRecentlyEndedEventsAsync(
            ApplicationDbContext context,
            ICertificateService certificateService,
            IVideoProgressService videoProgressService,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var windowStart = DateTime.Now.AddMinutes(-15);
            var recentlyEndedEvents = await context.Events
                .Include(e => e.Attendees)
                .Where(e => e.EndTime <= DateTime.Now && e.EndTime >= windowStart)
                .ToListAsync(cancellationToken);

            foreach (var @event in recentlyEndedEvents)
            {
                logger.LogInformation("Processing certificates for recently ended event: {Title}", @event.Title);
                foreach (var attendee in @event.Attendees)
                {
                    await ProcessAttendeeAsync(
                        attendee, @event, videoProgressService, certificateService, context, logger, cancellationToken);
                }
            }
        }

        private static async Task RecheckPendingCertificatesAsync(
            ApplicationDbContext context,
            ICertificateService certificateService,
            IVideoProgressService videoProgressService,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var endedEvents = await context.Events
                .Include(e => e.Attendees)
                .Where(e => e.EndTime <= DateTime.Now &&
                            e.Attendees.Any(a => a.IsEligibleForCertificate &&
                                !context.Certificates.Any(c => c.AttendeeId == a.Id && c.EventId == e.Id)))
                .ToListAsync(cancellationToken);

            foreach (var @event in endedEvents)
            {
                logger.LogInformation("Rechecking certificates for event: {Title}", @event.Title);
                var eligibleAttendees = @event.Attendees
                    .Where(a => a.IsEligibleForCertificate &&
                                !context.Certificates.Any(c => c.AttendeeId == a.Id && c.EventId == @event.Id));

                foreach (var attendee in eligibleAttendees)
                {
                    await ProcessAttendeeAsync(
                        attendee, @event, videoProgressService, certificateService, context, logger, cancellationToken);
                }
            }
        }

        private static async Task ProcessAttendeeAsync(
            Attendee attendee,
            Event @event,
            IVideoProgressService videoProgressService,
            ICertificateService certificateService,
            ApplicationDbContext context,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            try
            {
                var isEligible = await videoProgressService.CheckCertificateEligibilityAsync(
                    attendee.Id, @event.Id);

                if (!isEligible)
                {
                    return;
                }

                var existingCertificate = await context.Certificates
                    .FirstOrDefaultAsync(
                        c => c.AttendeeId == attendee.Id && c.EventId == @event.Id,
                        cancellationToken);

                if (existingCertificate != null)
                {
                    return;
                }

                var certificate = await certificateService.GenerateCertificateAsync(attendee, @event);
                var emailSent = await certificateService.SendCertificateEmailAsync(certificate);
                if (emailSent)
                {
                    logger.LogInformation(
                        "Certificate sent to {Email} for event {Title}",
                        attendee.Email,
                        @event.Title);
                }
                else
                {
                    logger.LogWarning(
                        "Certificate created but email not sent to {Email} for event {Title}",
                        attendee.Email,
                        @event.Title);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Certificate processing failed for {Email} in event {Title}",
                    attendee.Email,
                    @event.Title);
            }
        }
    }
}
