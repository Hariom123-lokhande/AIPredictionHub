using Microsoft.ML.Data;

namespace AIPredictionHub.Models.Rainfall
{
    public class RainfallPrediction
    {
        [ColumnName("Score")]
        public float PredictedRainfall { get; set; }
    }
}
