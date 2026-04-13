using System.ComponentModel.DataAnnotations;

namespace AIPredictionHub.Models.Laptop
{
    public class LaptopInputModel
    {
        [Required]
        public string? Brand { get; set; }

        [Required]
        [Range(2, 128, ErrorMessage = "RAM must be between 2GB and 128GB")]
        public float? RAM { get; set; }

        [Required]
        [Range(32, 4000, ErrorMessage = "Storage must be between 32GB and 4000GB")]
        public float? Storage { get; set; }

        [Required]
        public string? Processor { get; set; }

        [Required]
        [Range(10, 20, ErrorMessage = "Screen size must be between 10 and 20 inches")]
        public float? ScreenSize { get; set; }
    }
}
