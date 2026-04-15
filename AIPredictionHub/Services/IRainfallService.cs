using AIPredictionHub.Models.Rainfall;

namespace AIPredictionHub.Services
{
    public interface IRainfallService
    {
        Task TrainModelAsync();

        Task<(RainfallPrediction prediction, RainfallMetrics metrics)> PredictAsync(RainfallData input);

        RainfallMetrics GetMetrics();
    }
}
