
namespace DACSWEBSK.Models
{
    public class Question
    {
        public int Id { get; set; }

        public string Content { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string AskedBy { get; set; }

        public string? Reply { get; set; }
        public DateTime? RepliedAt { get; set; }
        public string? RepliedBy { get; set; }

        public int EventId { get; set; }

        public Event Event { get; set; }
    }

}
