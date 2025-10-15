using System.ComponentModel.DataAnnotations;
namespace Application.DTO
{
    public class CreateDeviceDto
    {
        [Required(ErrorMessage = "Device name is required.")]
        [MinLength(2, ErrorMessage = "Device name must be at least 2 characters long.")]
        [MaxLength(100, ErrorMessage = "Device name cannot exceed 100 characters.")]
        [RegularExpression(@"^(?!\d+$)[a-zA-Z0-9 _-]+$", ErrorMessage = "Device name must contain letters, numbers, spaces, hyphens, or underscores and cannot be only numbers.")]
        public string DeviceName { get; set; } = string.Empty;

        [MaxLength(250, ErrorMessage = "Description cannot exceed 250 characters.")]
        public string? Description { get; set; }
    }
}
