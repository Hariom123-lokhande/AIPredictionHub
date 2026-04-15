using AIPredictionHub.Models.Laptop;

namespace AIPredictionHub.Services
{
    public interface ILaptopService
    {
        bool CsvNotFound { get; }

        Task TrainModelAsync();

        Task TrainWithDummyDataAsync();

        Task<(LaptopPrediction prediction, LaptopMetrics metrics)> PredictAsync(LaptopData input);

        LaptopMetrics GetMetrics();
    }
}
