using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DACSWEBSK.Models
{
    public class SubtitleTranslation
    {
        public int Id { get; set; }

        [Required]
        public int VideoId { get; set; }

        [ForeignKey("VideoId")]
        public Video Video { get; set; }

        [Required]
        [StringLength(10)]
        public string TargetLanguage { get; set; } // Mã ngôn ngữ: en, vi, fr, de, es, ja, ko, zh, etc.

        [Required]
        public string TranslatedText { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }
    }
}

