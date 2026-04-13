using Microsoft.ML.Data;

namespace AIPredictionHub.Models.Fitness
{
    public class FitnessPrediction
    {
        [ColumnName("Score")]
        public float CaloriesBurned { get; set; }
    }
}
