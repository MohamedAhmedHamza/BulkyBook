using System.ComponentModel.DataAnnotations;

namespace BulkyBook.Models.ViewModels
{
    public class VerifyOtpVM
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter the 6-digit code.")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "The code must be exactly 6 digits.")]
        public string Code { get; set; } = string.Empty;
    }
}
