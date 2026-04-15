namespace AIPredictionHub.Models.Laptop
{
    public class LaptopViewModel
    {
        public LaptopInputModel Input { get; set; } = new();
        public LaptopPrediction? Prediction { get; set; }
        public LaptopMetrics? Metrics { get; set; }
        public bool IsPredicted { get; set; } = false;

        public bool CsvNotFound { get; set; } = false;
    }
}
