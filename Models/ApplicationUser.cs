using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace DACSWEBSK.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        public string FullName { get; set; }

        public DateTime? DateOfBirth { get; set; }

        public string? Address { get; set; }

        public string? PhoneNumber { get; set; }

        public string? Gender { get; set; }
        public int Points { get; set; } = 0;

        // Thêm các trường xác nhận email
        public string? EmailConfirmationCode { get; set; }
        public bool IsEmailConfirmed { get; set; } = false;
    }
}
