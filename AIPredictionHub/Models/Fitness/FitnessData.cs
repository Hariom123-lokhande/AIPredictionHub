using Microsoft.ML.Data;
namespace AIPredictionHub.Models.Fitness
{
    using System.ComponentModel.DataAnnotations;

    public class FitnessData
    {
        [LoadColumn(0)]
        [Required]
        [Range(20, 300, ErrorMessage = "Weight must be between 20kg and 300kg")]
        public float Weight { get; set; }
        [LoadColumn(1)]
        [Required]
        [Range(1, 300, ErrorMessage = "Duration must be between 1 and 300 minutes")]
        public float Duration { get; set; }

        [LoadColumn(2)]
        [Required]
        [Range(40, 220, ErrorMessage = "Heart rate must be between 40 and 220 bpm")]
        public float HeartRate { get; set; }

        [LoadColumn(3)]
        [Required]
        public string? ExerciseType { get; set; }


        [LoadColumn(4)]
        [ColumnName("Label")]
        public float CaloriesBurned { get; set; }
    }
}
