using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DACSWEBSK.Models
{
    public class AssignmentSubmission
    {
        public int Id { get; set; }

        [Required]
        public int AssignmentId { get; set; }

        [ForeignKey("AssignmentId")]
        public EventAssignment? Assignment { get; set; }

        [Required]
        public int AttendeeId { get; set; }

        [ForeignKey("AttendeeId")]
        public Attendee? Attendee { get; set; }

        [StringLength(10000)]
        [Display(Name = "Nội dung văn bản")]
        public string? TextContent { get; set; }

        [StringLength(500)]
        [Display(Name = "Đường dẫn file")]
        public string? FilePath { get; set; }

        [StringLength(255)]
        [Display(Name = "Tên file")]
        public string? FileName { get; set; }

        [Display(Name = "Điểm số")]
        [Range(0, 1000)]
        public decimal? Score { get; set; }

        [StringLength(1000)]
        [Display(Name = "Nhận xét")]
        public string? Feedback { get; set; }

        [Display(Name = "Trạng thái")]
        [StringLength(50)]
        public string Status { get; set; } = "Submitted"; // Submitted, Graded, Returned

        [Display(Name = "Ngày nộp")]
        [DataType(DataType.DateTime)]
        public DateTime SubmittedAt { get; set; } = DateTime.Now;

        [Display(Name = "Ngày chấm điểm")]
        [DataType(DataType.DateTime)]
        public DateTime? GradedAt { get; set; }

        [StringLength(100)]
        [Display(Name = "Người chấm")]
        public string? GradedBy { get; set; }
    }
}

