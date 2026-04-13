using System.ComponentModel.DataAnnotations;

namespace AIPredictionHub.Models.Fitness
{
    public class FitnessInputModel
    {
        [Required]
        [Range(20, 300)]
        public float? Weight { get; set; }
        
        [Required]
        [Range(1, 300)]
        public float? Duration { get; set; }

        [Required]
        [Range(40, 220)]
        public float? HeartRate { get; set; }

        [Required]
        public string? ExerciseType { get; set; }
    }
}