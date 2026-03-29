using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DACSWEBSK.Models
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Event> Events { get; set; }
        public DbSet<Attendee> Attendees { get; set; }
        public DbSet<ScheduleItem> ScheduleItems { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<Video> Videos { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<Gift> Gifts { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<GiftRedemption> GiftRedemptions { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<Certificate> Certificates { get; set; }
        public DbSet<VideoProgress> VideoProgresses { get; set; }
        public DbSet<EventAssignment> EventAssignments { get; set; }
        public DbSet<AssignmentSubmission> AssignmentSubmissions { get; set; }
        public DbSet<OfflineAttendanceEvidence> OfflineAttendanceEvidences { get; set; }
        public DbSet<SubtitleTranslation> SubtitleTranslations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // VideoProgress relationships - ensure both relationships use NoAction
            modelBuilder.Entity<VideoProgress>()
                .HasOne(vp => vp.Video)
                .WithMany(v => v.VideoProgresses)
                .HasForeignKey(vp => vp.VideoId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<VideoProgress>()
                .HasOne(vp => vp.Attendee)
                .WithMany(a => a.VideoProgresses)
                .HasForeignKey(vp => vp.AttendeeId)
                .OnDelete(DeleteBehavior.NoAction);

            // Certificate relationships - fixed duplicate relationship
            modelBuilder.Entity<Certificate>()
                .HasOne(c => c.Attendee)
                .WithMany(a => a.Certificates)
                .HasForeignKey(c => c.AttendeeId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Certificate>()
                .HasOne(c => c.Event)
                .WithMany(e => e.Certificates)
                .HasForeignKey(c => c.EventId)
                .OnDelete(DeleteBehavior.NoAction);

            // Attendee - Event relationship
            modelBuilder.Entity<Attendee>()
                .HasOne(a => a.Event)
                .WithMany(e => e.Attendees)
                .HasForeignKey(a => a.EventId)
                .OnDelete(DeleteBehavior.NoAction);

            // Event - Location relationship
            modelBuilder.Entity<Event>()
                .HasOne(e => e.Location)
                .WithMany(l => l.Events)
                .HasForeignKey(e => e.LocationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Question - Event relationship
            modelBuilder.Entity<Question>()
                .HasOne(q => q.Event)
                .WithMany(e => e.Questions)
                .HasForeignKey(q => q.EventId)
                .OnDelete(DeleteBehavior.NoAction);

            // ScheduleItem - Event relationship
            modelBuilder.Entity<ScheduleItem>()
                .HasOne(s => s.Event)
                .WithMany(e => e.Schedule)
                .HasForeignKey(s => s.EventId)
                .OnDelete(DeleteBehavior.NoAction);

            // Video - Event relationship
            modelBuilder.Entity<Video>()
                .HasOne(v => v.Event)
                .WithMany(e => e.Videos)
                .HasForeignKey(v => v.EventId)
                .OnDelete(DeleteBehavior.NoAction);

            // GiftRedemption - Gift relationship
            modelBuilder.Entity<GiftRedemption>()
                .HasOne(gr => gr.Gift)
                .WithMany()
                .HasForeignKey(gr => gr.GiftId)
                .OnDelete(DeleteBehavior.NoAction);

            // Notification - Event relationship
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.Event)
                .WithMany(e => e.Notifications)
                .HasForeignKey(n => n.EventId)
                .OnDelete(DeleteBehavior.NoAction);

            // EventAssignment - Event relationship
            modelBuilder.Entity<EventAssignment>()
                .HasOne(ea => ea.Event)
                .WithMany(e => e.Assignments)
                .HasForeignKey(ea => ea.EventId)
                .OnDelete(DeleteBehavior.NoAction);

            // AssignmentSubmission - EventAssignment relationship
            modelBuilder.Entity<AssignmentSubmission>()
                .HasOne(asub => asub.Assignment)
                .WithMany(a => a.Submissions)
                .HasForeignKey(asub => asub.AssignmentId)
                .OnDelete(DeleteBehavior.NoAction);

            // AssignmentSubmission - Attendee relationship
            modelBuilder.Entity<AssignmentSubmission>()
                .HasOne(asub => asub.Attendee)
                .WithMany()
                .HasForeignKey(asub => asub.AttendeeId)
                .OnDelete(DeleteBehavior.NoAction);

            // OfflineAttendanceEvidence - Event relationship
            modelBuilder.Entity<OfflineAttendanceEvidence>()
                .HasOne(oe => oe.Event)
                .WithMany()
                .HasForeignKey(oe => oe.EventId)
                .OnDelete(DeleteBehavior.NoAction);

            // OfflineAttendanceEvidence - Attendee relationship
            modelBuilder.Entity<OfflineAttendanceEvidence>()
                .HasOne(oe => oe.Attendee)
                .WithMany()
                .HasForeignKey(oe => oe.AttendeeId)
                .OnDelete(DeleteBehavior.NoAction);

            // SubtitleTranslation - Video relationship
            modelBuilder.Entity<SubtitleTranslation>()
                .HasOne(st => st.Video)
                .WithMany()
                .HasForeignKey(st => st.VideoId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Attendee>().HasQueryFilter(a => !a.IsDeleted);
        }
    }
}
