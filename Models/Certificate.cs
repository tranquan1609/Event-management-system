using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DACSWEBSK.Models
{
    public class Certificate
    {
        public int Id { get; set; }

        [Required]
        public string CertificateNumber { get; set; }

        [Required]
        public int AttendeeId { get; set; }

        [ForeignKey("AttendeeId")]
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public Attendee Attendee { get; set; }

        [Required]
        public int EventId { get; set; }

        [ForeignKey("EventId")]
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public Event Event { get; set; }

        [Required]
        public DateTime IssueDate { get; set; }

        public string? CertificateUrl { get; set; }

        public bool IsEmailSent { get; set; } = false;

        public DateTime? EmailSentDate { get; set; }
    }
} 