using System.ComponentModel.DataAnnotations;

namespace DACSWEBSK.Models
{
    public class Location
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }  // Location name

        public string? Address { get; set; }

        public string? Description { get; set; }

        public ICollection<Event>? Events { get; set; }
    }
}
