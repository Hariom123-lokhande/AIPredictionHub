using AIPredictionHub.Models.Fitness;

namespace AIPredictionHub.Services
{
    public interface IFitnessService
    {
        Task TrainModelAsync();

        Task<(FitnessPrediction prediction, FitnessMetrics metrics)> PredictAsync(FitnessData input);

        FitnessMetrics GetMetrics();
    }
}
