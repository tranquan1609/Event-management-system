using System;
using System.ComponentModel.DataAnnotations;

namespace DACSWEBSK.Models
{
    public enum VideoType
    {
        Video,
        Livestream
    }

    public class Video
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; }

        [Required]
        public string Url { get; set; } // YouTube / Facebook livestream link

        public string? LocalFilePath { get; set; } // Đường dẫn file video local

        public string? Description { get; set; } // Mô tả video

        public string? Summary { get; set; } // Tóm tắt nội dung

        public DateTime UploadDate { get; set; } = DateTime.Now;

        [Required]
        public int Duration { get; set; } // Thời lượng video (giây)

        [Required]
        public VideoType Type { get; set; } = VideoType.Video;

        public DateTime? StartTime { get; set; } // Thời gian bắt đầu cho livestream

        public DateTime? EndTime { get; set; } // Thời gian kết thúc cho livestream

        [Required]
        public bool IsRequired { get; set; } = true; // Có bắt buộc xem để nhận chứng nhận không

        [Required]
        public int EventId { get; set; }

        public Event? Event { get; set; }

        public string? Transcript { get; set; }

        public ICollection<VideoProgress> VideoProgresses { get; set; } = new List<VideoProgress>();
    }
}
