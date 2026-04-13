using Microsoft.ML;
using Microsoft.ML.Data;
using AIPredictionHub.Models.Fitness;
using Microsoft.Extensions.Logging;

namespace AIPredictionHub.Services
{
    public class FitnessService
    {
        private readonly MLContext _mlContext;
        private readonly ILogger<FitnessService> _logger;
        private ITransformer _model = null!;
        private PredictionEngine<FitnessData, FitnessPrediction> _predictionEngine = null!;
        private FitnessMetrics _metrics = null!;

        public FitnessService(MLContext mlContext, ILogger<FitnessService> logger) //dependency injection
        {
            _mlContext = mlContext;
            _logger = logger;
            _logger.LogInformation("Initializing FitnessService and training model");
            TrainModel();
        }
        private IDataView LoadAndCleanData(string path)
        {
            _logger.LogInformation("Loading and cleaning data from {FileName}", Path.GetFileName(path));
            var rawData = _mlContext.Data.LoadFromTextFile<FitnessData>(
                path: path,
                hasHeader: true,
                separatorChar: ',');

            var cleanedList = _mlContext.Data
                .CreateEnumerable<FitnessData>(rawData, reuseRowObject: false)

                //Remove nulls / NaN
                .Where(x =>
                    x != null &&
                    !float.IsNaN(x.Weight) &&
                    !float.IsNaN(x.Duration) &&
                    !float.IsNaN(x.HeartRate) &&
                    !string.IsNullOrWhiteSpace(x.ExerciseType) &&
                    !float.IsNaN(x.CaloriesBurned))

                //Remove invalid ranges
                .Where(x =>
                    x.Weight >= 20 && x.Weight <= 300 &&
                    x.Duration >= 1 && x.Duration <= 300 &&
                    x.HeartRate >= 40 && x.HeartRate <= 220 &&
                    x.CaloriesBurned >= 0)

                //Remove duplicates
                .GroupBy(x => g(x))
                .Select(g => g.First())

                .ToList();

            _logger.LogInformation("Data cleaning complete. {Count} records remain", cleanedList.Count);
            return _mlContext.Data.LoadFromEnumerable(cleanedList);
        }

        private object g(FitnessData x) => new
        {
            x.Weight,
            x.Duration,
            x.HeartRate,
            x.ExerciseType,
            x.CaloriesBurned
        };
      //train     
        private void TrainModel()
        {
            try
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(), "Data", "fitness.csv");

                if (!File.Exists(path))
                {
                    _logger.LogError("Fitness training data not found: {FileName}", Path.GetFileName(path));
                    throw new FileNotFoundException($"{Path.GetFileName(path)} not found");
                }

                var cleanedData = LoadAndCleanData(path);

                var count = _mlContext.Data.CreateEnumerable<FitnessData>(cleanedData, false).Count();
                if (count < 5)
                {
                    _logger.LogWarning("Not enough valid data after cleaning ({Count} records)", count);
                    throw new Exception("Not enough valid data after cleaning");
                }

                _logger.LogInformation("Building ML pipeline and fitting Fitness model");
                var split = _mlContext.Data.TrainTestSplit(cleanedData, testFraction: 0.2);

                var pipeline = _mlContext.Transforms

                    //Categorical encoding
                    .Categorical.OneHotEncoding("ExerciseTypeEncoded", nameof(FitnessData.ExerciseType))

                    // Features combine
                    .Append(_mlContext.Transforms.Concatenate("Features",
                        nameof(FitnessData.Weight),
                        nameof(FitnessData.Duration),
                        nameof(FitnessData.HeartRate),
                        "ExerciseTypeEncoded"))

                    //Missing values
                    .Append(_mlContext.Transforms.ReplaceMissingValues("Features"))
                    .Append(_mlContext.Transforms.NormalizeMinMax("Features"))

                    .Append(_mlContext.Regression.Trainers.LightGbm(
                        labelColumnName: "Label",
                        featureColumnName: "Features"));

                _model = pipeline.Fit(split.TrainSet);
                _predictionEngine = _mlContext.Model.CreatePredictionEngine<FitnessData, FitnessPrediction>(_model);

                //evaluation
              
                var predictions = _model.Transform(split.TestSet);
                var metrics = _mlContext.Regression.Evaluate(predictions);

                // Save the trained model to a .zip file
                string modelPath = Path.Combine(Directory.GetCurrentDirectory(), "Models", "Fitness", "fitness_model.zip");
                Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
                _mlContext.Model.Save(_model, split.TrainSet.Schema, modelPath);
                _logger.LogInformation("Fitness model saved as {FileName}", Path.GetFileName(modelPath));

                _metrics = new FitnessMetrics
                {
                    RMSE = Math.Round(metrics.RootMeanSquaredError, 3),
                    MAE = Math.Round(metrics.MeanAbsoluteError, 3),
                    R2 = Math.Round(metrics.RSquared, 3)
                };
                _logger.LogInformation("Fitness model metrics: RMSE={RMSE}, MAE={MAE}, R2={R2}", _metrics.RMSE, _metrics.MAE, _metrics.R2);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fitness model training failed");
                throw new Exception($"Model training failed: {ex.Message}");
            }
        }

        //prediction
        public (FitnessPrediction prediction, FitnessMetrics metrics) Predict(FitnessData input)
        {
            if (input == null)
            {
                _logger.LogWarning("Prediction failed: Input was null");
                throw new ArgumentNullException(nameof(input));
            }

            if (_model == null || _predictionEngine == null)
            {
                _logger.LogWarning("Prediction failed: Model or PredictionEngine is null");
                throw new Exception("Model not trained");
            }

            ValidateInput(input);

            try
            {
                _logger.LogDebug("Running prediction for input: {@Input}", input);
                var result = _predictionEngine.Predict(input);

                // ❌ Negative calories fix
                if (result.CaloriesBurned < 0)
                {
                    result.CaloriesBurned = 0;
                }

                return (result, _metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Prediction execution failed");
                throw new Exception($"Prediction failed: {ex.Message}");
            }
        }

        //input validation
        private void ValidateInput(FitnessData input)
        {
            if (input.Weight < 20 || input.Weight > 300)
            {
                throw new ArgumentException("Invalid weight");
            }
            if (input.Duration <= 0 || input.Duration > 300)
            {
                throw new ArgumentException("Invalid duration");
            }
            if (input.HeartRate < 40 || input.HeartRate > 220)
            {
                throw new ArgumentException("Invalid heart rate");
            }
            if (string.IsNullOrWhiteSpace(input.ExerciseType))
            {
                throw new ArgumentException("Exercise type required");
            }
        }

        //metrics
        public FitnessMetrics GetMetrics()
        {
            if (_metrics == null)
            {
                throw new Exception("Metrics not available");
            }

            return _metrics;
        }
    }
}