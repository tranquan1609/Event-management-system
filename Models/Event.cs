using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DACSWEBSK.Models
{
    public class Event
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

        [Required]
        [DataType(DataType.DateTime)]
        public DateTime StartTime { get; set; }

        [Required]
        [DataType(DataType.DateTime)]
        public DateTime EndTime { get; set; }

        [Required]
        public int LocationId { get; set; }

        [ForeignKey("LocationId")]
        public Location? Location { get; set; }

        [StringLength(500)]
        public string? ImageUrl { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Upcoming";

        // Navigation properties
        public ICollection<Attendee> Attendees { get; set; } = new List<Attendee>(); // Khởi tạo
        public ICollection<ScheduleItem> Schedule { get; set; } = new List<ScheduleItem>(); // Khởi tạo
        public ICollection<Question>? Questions { get; set; }
        public ICollection<Video>? Videos { get; set; }
        public ICollection<Notification>? Notifications { get; set; }
        public ICollection<Certificate>? Certificates { get; set; }
        public ICollection<EventAssignment>? Assignments { get; set; }
    }
}