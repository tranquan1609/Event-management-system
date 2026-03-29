using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DACSWEBSK.Models
{ 
  public class Attendee
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Họ tên phải từ 3 đến 100 ký tự")]
        [Display(Name = "Họ tên")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        [Display(Name = "Số điện thoại")]
        public string? Phone { get; set; }

        public string? TicketStatus { get; set; } = "Pending";

        [Required]
        public int EventId { get; set; }

        [ForeignKey("EventId")]
        public Event? Event { get; set; }

        public virtual ICollection<Certificate>? Certificates { get; set; }

        public bool IsEligibleForCertificate { get; set; } = false;

        public ICollection<VideoProgress> VideoProgresses { get; set; } = new List<VideoProgress>();

        public bool IsDeleted { get; set; } = false;
    }
}