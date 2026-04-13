using Microsoft.ML;
using Microsoft.ML.Data;
using AIPredictionHub.Models.Rainfall;
using Microsoft.Extensions.Logging;

namespace AIPredictionHub.Services
{
    public class RainfallService
    {
        private readonly MLContext _mlContext;
        private readonly ILogger<RainfallService> _logger;
        private ITransformer _model = null!;
        private PredictionEngine<RainfallData, RainfallPrediction> _predictionEngine = null!;
        private RainfallMetrics _metrics = null!;

        public RainfallService(MLContext mlContext, ILogger<RainfallService> logger) //dependency injection
        {
            _mlContext = mlContext;
            _logger = logger;
            _logger.LogInformation("Initializing RainfallService and training model");
            TrainModel();
        }

        // data cleaning
        private IDataView LoadAndCleanData(string path)
        {
            _logger.LogInformation("Loading and cleaning data from {FileName}", Path.GetFileName(path));
            var rawData = _mlContext.Data.LoadFromTextFile<RainfallData>(
                path: path,
                hasHeader: true,
                separatorChar: ',');

            var cleanedList = _mlContext.Data
                .CreateEnumerable<RainfallData>(rawData, reuseRowObject: false)

                //Remove nulls
                .Where(x =>
                    x != null &&
                    !float.IsNaN(x.Temperature) &&
                    !float.IsNaN(x.Humidity) &&
                    !float.IsNaN(x.WindSpeed) &&
                    !float.IsNaN(x.Pressure) &&
                    !float.IsNaN(x.Rainfall))

                //Remove invalid ranges
                .Where(x =>
                    x.Temperature >= -50 && x.Temperature <= 60 &&
                    x.Humidity >= 0 && x.Humidity <= 100 &&
                    x.WindSpeed >= 0 && x.WindSpeed <= 300 &&
                    x.Pressure >= 800 && x.Pressure <= 1100 &&
                    x.Rainfall >= 0)

                //Remove duplicates
                .GroupBy(x => g(x))
                .Select(g => g.First())

                .ToList();

            _logger.LogInformation("Data cleaning complete. {Count} records remain", cleanedList.Count);
            return _mlContext.Data.LoadFromEnumerable(cleanedList);
        }

        private object g(RainfallData x) => new
        {
            x.Temperature,
            x.Humidity,
            x.WindSpeed,
            x.Pressure,
            x.Rainfall
        };

        private void TrainModel()
        {
            try
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(), "Data", "rainfall.csv");

                //for check file
                if (!File.Exists(path))
                {
                    _logger.LogError("Rainfall training data not found: {FileName}", Path.GetFileName(path));
                    throw new FileNotFoundException($"{Path.GetFileName(path)} not found in Data folder");
                }

                //load and clean
                var cleanedData = LoadAndCleanData(path);

                //data is not enough after ytraining
                var count = _mlContext.Data.CreateEnumerable<RainfallData>(cleanedData, false).Count();
                if (count < 5)
                {
                    _logger.LogWarning("Not enough valid data after cleaning ({Count} records)", count);
                    throw new Exception("Not enough valid data after cleaning");
                }

                _logger.LogInformation("Building ML pipeline and fitting Rainfall model");
                //dat split happens
                var split = _mlContext.Data.TrainTestSplit(cleanedData, testFraction: 0.2);

                var pipeline = _mlContext.Transforms
                    .Concatenate("Features",
                        nameof(RainfallData.Temperature),
                        nameof(RainfallData.Humidity),
                        nameof(RainfallData.WindSpeed),
                        nameof(RainfallData.Pressure))

                    //Missingvalues handling
                    .Append(_mlContext.Transforms.ReplaceMissingValues("Features"))

                    // Normalization
                    .Append(_mlContext.Transforms.NormalizeMinMax("Features"))
                    .Append(_mlContext.Regression.Trainers.LightGbm(
                        labelColumnName: "Label",
                        featureColumnName: "Features"));

                _model = pipeline.Fit(split.TrainSet);
                _predictionEngine = _mlContext.Model.CreatePredictionEngine<RainfallData, RainfallPrediction>(_model);

                //evaluate
                var predictions = _model.Transform(split.TestSet);
                var metrics = _mlContext.Regression.Evaluate(predictions);

                // Save the trained model to a .zip file
                string modelPath = Path.Combine(Directory.GetCurrentDirectory(), "Models", "Rainfall", "rainfall_model.zip");
                Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
                _mlContext.Model.Save(_model, split.TrainSet.Schema, modelPath);
                _logger.LogInformation("Rainfall model saved as {FileName}", Path.GetFileName(modelPath));

                _metrics = new RainfallMetrics
                {
                    RMSE = Math.Round(metrics.RootMeanSquaredError, 3),
                    MAE = Math.Round(metrics.MeanAbsoluteError, 3),
                    R2 = Math.Round(metrics.RSquared, 3)
                };
                _logger.LogInformation("Rainfall model metrics: RMSE={RMSE}, MAE={MAE}, R2={R2}", _metrics.RMSE, _metrics.MAE, _metrics.R2);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rainfall model training failed");
                throw new Exception($"Model training failed: {ex.Message}");
            }
        }
        //prdiction
        public (RainfallPrediction prediction, RainfallMetrics metrics) Predict(RainfallData input)
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

                //Negative rainfall doesn't make sense
                if (result.PredictedRainfall < 0)
                {
                    result.PredictedRainfall = 0;
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
        private void ValidateInput(RainfallData input)
        {
            if (input.Temperature < -50 || input.Temperature > 60)
            {
                throw new ArgumentException("Temperature out of range");
            }

            if (input.Humidity < 0 || input.Humidity > 100)
            {
                throw new ArgumentException("Invalid humidity");
            }

            if (input.WindSpeed < 0 || input.WindSpeed > 300)
            {
                throw new ArgumentException("Invalid wind speed");
            }

            if (input.Pressure < 800 || input.Pressure > 1100)
            {
                throw new ArgumentException("Invalid pressure");
            }
        }

        // get metrics
        public RainfallMetrics GetMetrics()
        {
            if (_metrics == null)
            {
                throw new Exception("Metrics not available");
            }

            return _metrics;
        }
    }
}