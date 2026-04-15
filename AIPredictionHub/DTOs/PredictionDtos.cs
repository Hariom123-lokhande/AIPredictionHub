namespace AIPredictionHub.DTOs
{
    // --- Rainfall DTOs ---
    public class RainfallRequestDto
    {
        public float Temperature { get; set; }
        public float Humidity { get; set; }
        public float WindSpeed { get; set; }
        public float Pressure { get; set; }
    }

    public class RainfallResponseDto
    {
        public float PredictedRainfall { get; set; }
        public double RMSE { get; set; }
        public double R2 { get; set; }
    }

    // --- Fitness DTOs ---
    public class FitnessRequestDto
    {
        public float Weight { get; set; }
        public float Duration { get; set; }
        public float HeartRate { get; set; }
        public string ExerciseType { get; set; } = string.Empty;
    }

    public class FitnessResponseDto
    {
        public float CaloriesBurned { get; set; }
        public double RMSE { get; set; }
        public double R2 { get; set; }
    }

    // --- Laptop DTOs ---
    public class LaptopRequestDto
    {
        public string Brand { get; set; } = string.Empty;
        public string Processor { get; set; } = string.Empty;
        public float RAM { get; set; }
        public float Storage { get; set; }
        public float ScreenSize { get; set; }
    }

    public class LaptopResponseDto
    {
        public float PredictedPrice { get; set; }
        public double RMSE { get; set; }
        public double R2 { get; set; }
    }
}
