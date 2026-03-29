using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DACSWEBSK.Models
{
    public class OfflineAttendanceEvidence
    {
        public int Id { get; set; }

        [Required]
        public int EventId { get; set; }

        [ForeignKey("EventId")]
        public Event? Event { get; set; }

        [Required]
        public int AttendeeId { get; set; }

        [ForeignKey("AttendeeId")]
        public Attendee? Attendee { get; set; }

        [Required]
        [StringLength(500)]
        [Display(Name = "Đường dẫn file")]
        public string FilePath { get; set; }

        [Required]
        [StringLength(255)]
        [Display(Name = "Tên file")]
        public string FileName { get; set; }

        [StringLength(50)]
        [Display(Name = "Loại file")]
        public string? FileType { get; set; }

        [Display(Name = "Kích thước file (bytes)")]
        public long FileSize { get; set; }

        [StringLength(50)]
        [Display(Name = "Trạng thái")]
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected

        [StringLength(1000)]
        [Display(Name = "Ghi chú từ chối")]
        public string? RejectionReason { get; set; }

        [Display(Name = "Ngày upload")]
        [DataType(DataType.DateTime)]
        public DateTime UploadedAt { get; set; } = DateTime.Now;

        [Display(Name = "Ngày xử lý")]
        [DataType(DataType.DateTime)]
        public DateTime? ProcessedAt { get; set; }

        [StringLength(100)]
        [Display(Name = "Người xử lý")]
        public string? ProcessedBy { get; set; }
    }
}

