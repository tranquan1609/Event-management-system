using System.ComponentModel.DataAnnotations;

namespace DACSWEBSK.Models
{
    public class Gift
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }  // Gift name

        public string? Description { get; set; }

        public string? ImageUrl { get; set; }

        public int? RequiredPoints { get; set; }  // For point-based redemption

        public bool IsRandomCode { get; set; }  // For instant code-based rewards
    }
}
