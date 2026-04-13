namespace AIPredictionHub.Models.Rainfall
{
    public class RainfallViewModel
    {
        public RainfallInputModel Input { get; set; } = new();
        public RainfallPrediction? Prediction { get; set; }
        public RainfallMetrics? Metrics { get; set; }
        public bool IsPredicted { get; set; } = false;
    }
}
