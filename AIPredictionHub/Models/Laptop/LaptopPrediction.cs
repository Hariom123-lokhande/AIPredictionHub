using Microsoft.ML.Data;

namespace AIPredictionHub.Models.Laptop
{
    public class LaptopPrediction
    {
        [ColumnName("Score")]
        public float PredictedPrice { get; set; }
    }
}
