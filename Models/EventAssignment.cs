using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DACSWEBSK.Models
{
    public class EventAssignment
    {
        public int Id { get; set; }

        [Required]
        public int EventId { get; set; }

        [ForeignKey("EventId")]
        public Event? Event { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "Tiêu đề")]
        public string Title { get; set; }

        [StringLength(5000)]
        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        [StringLength(5000)]
        [Display(Name = "Hướng dẫn")]
        public string? Instructions { get; set; }

        [Display(Name = "Hạn nộp bài")]
        [DataType(DataType.DateTime)]
        public DateTime? DueDate { get; set; }

        [Display(Name = "Điểm tối đa")]
        [Range(0, 1000)]
        public int? MaxScore { get; set; }

        [Display(Name = "Cho phép nộp file")]
        public bool AllowFileUpload { get; set; } = true;

        [Display(Name = "Cho phép nhập văn bản")]
        public bool AllowTextSubmission { get; set; } = true;

        [Display(Name = "Ngày tạo")]
        [DataType(DataType.DateTime)]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Ngày cập nhật")]
        [DataType(DataType.DateTime)]
        public DateTime? UpdatedAt { get; set; }

        // Navigation property
        public ICollection<AssignmentSubmission> Submissions { get; set; } = new List<AssignmentSubmission>();
    }
}

