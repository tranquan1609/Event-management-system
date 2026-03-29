using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DACSWEBSK.Models
{
    public class VideoProgress
    {
        public int Id { get; set; }

        [Required]
        public int VideoId { get; set; }

        [ForeignKey("VideoId")]
        public Video Video { get; set; }

        [Required]
        public int AttendeeId { get; set; }

        [ForeignKey("AttendeeId")]
        public Attendee Attendee { get; set; }

        [Required]
        public int ViewDuration { get; set; } // Thời lượng đã xem (giây)

        [Required]
        public double ViewPercentage { get; set; } // Phần trăm đã xem

        public DateTime LastViewedAt { get; set; } = DateTime.Now;

        public bool IsCompleted { get; set; } = false; // Đã hoàn thành xem video chưa (>= 70%)

        // Cho livestream
        public DateTime? JoinTime { get; set; } // Thời điểm tham gia
        public DateTime? LeaveTime { get; set; } // Thời điểm rời đi
        public int? TotalAttendanceTime { get; set; } // Tổng thời gian tham gia (giây)
        public double? AttendancePercentage { get; set; } // Phần trăm tham gia
    }
} 