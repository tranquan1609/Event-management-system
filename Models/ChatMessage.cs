using System.ComponentModel.DataAnnotations;

namespace DACSWEBSK.Models
{
    public class ChatMessage
    {
        [Key]
        public int Id { get; set; }

        // Cho phép nullable, không ràng buộc foreign key
        public string? UserId { get; set; }

        [Required]
        public string Message { get; set; }

        [Required]
        public bool IsFromUser { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
} 