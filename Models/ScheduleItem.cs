
namespace DACSWEBSK.Models
{
    public class ScheduleItem
    {
        public int Id { get; set; }

        public string Title { get; set; }

        public string Speaker { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public int EventId { get; set; }

        // Mối quan hệ
        public Event? Event { get; set; } // Để nullable để tránh lỗi binding
    }

}
