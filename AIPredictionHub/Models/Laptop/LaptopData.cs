using Microsoft.ML.Data;
namespace AIPredictionHub.Models.Laptop
{
    using System.ComponentModel.DataAnnotations;

    public class LaptopData
    {
        [LoadColumn(0)]
        [Required]
        public string? Brand { get; set; }

        [LoadColumn(1)]
        [Required]
        [Range(2, 128, ErrorMessage = "RAM must be between 2GB and 128GB")]
        public float RAM { get; set; }

        [LoadColumn(2)]
        [Required]
        [Range(32, 4000, ErrorMessage = "Storage must be between 32GB and 4000GB")]
        public float Storage { get; set; }


        [LoadColumn(3)]
        [Required]
        public string? Processor { get; set; }


        [LoadColumn(4)]
        [Required]
        [Range(10, 20, ErrorMessage = "Screen size must be between 10 and 20 inches")]
        public float ScreenSize { get; set; }

        [LoadColumn(5)]
        [ColumnName("Label")]
        public float Price { get; set; }
    }
}
