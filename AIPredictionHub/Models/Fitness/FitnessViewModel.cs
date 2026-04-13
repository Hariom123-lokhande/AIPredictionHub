
namespace AIPredictionHub.Models.Fitness
{
    public class FitnessViewModel
    {
        public FitnessInputModel Input { get; set; } = new();
        public FitnessPrediction? Prediction { get; set; }
        public FitnessMetrics? Metrics { get; set; }
        public bool IsPredicted { get; set; } = false;
    }
}
