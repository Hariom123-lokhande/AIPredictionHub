using Microsoft.ML;
using Microsoft.ML.Data;
using AIPredictionHub.Models.Rainfall;
using Microsoft.Extensions.Logging;

namespace AIPredictionHub.Services
{
    public class RainfallService : IRainfallService
    {
        private const string DATA_FILE_NAME = "rainfall.csv";
        private const string MODEL_FILE_NAME = "rainfall_model.zip";
        private const string DATA_FOLDER = "Data";
        private const string MODELS_FOLDER = "Models";
        private const string RAINFALL_SUBFOLDER = "Rainfall";

        private readonly MLContext _mlContext;
        private readonly ILogger<RainfallService> _logger;
        private readonly Lazy<Task> _initializationTask;
        private ITransformer _model = null!;
        private PredictionEngine<RainfallData, RainfallPrediction> _predictionEngine = null!;
        private RainfallMetrics _metrics = null!;

        public RainfallService(MLContext mlContext, ILogger<RainfallService> logger)
        {
            _mlContext = mlContext;
            _logger = logger;
            _logger.LogInformation("Initializing RainfallService and training model");
            _initializationTask = new Lazy<Task>(() => TrainModelAsync());
            _ = _initializationTask.Value;
        }

        private async Task EnsureModelInitializedAsync()
        {
            try
            {
                await _initializationTask.Value;
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, "Rainfall initialization failed due to missing data file");
                throw new InvalidOperationException("Rainfall model initialization failed because training data is missing.", ex);
            }
        }

        /// <summary>
        /// Loads data from CSV and cleans it by removing nulls, invalid ranges, and duplicates.
        /// </summary>
        private async Task<IDataView> LoadAndCleanDataAsync(string path)
        {
            _logger.LogInformation("Loading and cleaning data from {FileName}", Path.GetFileName(path));
            
            // Reading file is an I/O operation
            var rawDataList = await Task.Run(() => _mlContext.Data.LoadFromTextFile<RainfallData>(
                path: path,
                hasHeader: true,
                separatorChar: ','));

            var cleanedList = _mlContext.Data
                .CreateEnumerable<RainfallData>(rawDataList, reuseRowObject: false)
                .Where(data => (data != null) &&
                            (!float.IsNaN(data.Temperature)) &&
                            (!float.IsNaN(data.Humidity)) &&
                            (!float.IsNaN(data.WindSpeed)) &&
                            (!float.IsNaN(data.Pressure)) &&
                            (!float.IsNaN(data.Rainfall)))
                .Where(data => (data.Temperature >= -50 && data.Temperature <= 60) &&
                            (data.Humidity >= 0 && data.Humidity <= 100) &&
                            (data.WindSpeed >= 0 && data.WindSpeed <= 300) &&
                            (data.Pressure >= 800 && data.Pressure <= 1100) &&
                            (data.Rainfall >= 0))
                .GroupBy(data => new { data.Temperature, data.Humidity, data.WindSpeed, data.Pressure, data.Rainfall })
                .Select(group => group.First())
                .ToList();

            _logger.LogInformation("Data cleaning complete. {Count} records remain", cleanedList.Count);
            return _mlContext.Data.LoadFromEnumerable(cleanedList);
        }

        /// <summary>
        /// Trains the Rainfall prediction model using LightGBM.
        /// </summary>
        public async Task TrainModelAsync()
        {
            try
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(), DATA_FOLDER, DATA_FILE_NAME);

                if (!File.Exists(path))
                {
                    _logger.LogError("Rainfall training data not found: {FileName}", Path.GetFileName(path));
                    throw new FileNotFoundException("Training data file not found.", Path.GetFileName(path));
                }

                var cleanedData = await LoadAndCleanDataAsync(path);
                var count = _mlContext.Data.CreateEnumerable<RainfallData>(cleanedData, false).Count();
                
                if (count < 5)
                {
                    _logger.LogWarning("Not enough valid data after cleaning ({Count} records)", count);
                    throw new InvalidOperationException("Insufficient data for training.");
                }

                _logger.LogInformation("Building ML pipeline and fitting Rainfall model");
                var split = _mlContext.Data.TrainTestSplit(cleanedData, testFraction: 0.2);

                var pipeline = _mlContext.Transforms
                    .Concatenate("Features",
                        nameof(RainfallData.Temperature),
                        nameof(RainfallData.Humidity),
                        nameof(RainfallData.WindSpeed),
                        nameof(RainfallData.Pressure))
                    .Append(_mlContext.Transforms.ReplaceMissingValues("Features"))
                    .Append(_mlContext.Transforms.NormalizeMinMax("Features"))
                    .Append(_mlContext.Regression.Trainers.LightGbm(
                        labelColumnName: "Label",
                        featureColumnName: "Features"));

                await Task.Run(() => 
                {
                    _model = pipeline.Fit(split.TrainSet);
                    _predictionEngine = _mlContext.Model.CreatePredictionEngine<RainfallData, RainfallPrediction>(_model);

                    var predictions = _model.Transform(split.TestSet);
                    var evaluationMetrics = _mlContext.Regression.Evaluate(predictions);

                    _metrics = new RainfallMetrics
                    {
                        RMSE = Math.Round(evaluationMetrics.RootMeanSquaredError, 3),
                        MAE = Math.Round(evaluationMetrics.MeanAbsoluteError, 3),
                        R2 = Math.Round(evaluationMetrics.RSquared, 3)
                    };
                });

                string modelPath = Path.Combine(Directory.GetCurrentDirectory(), MODELS_FOLDER, RAINFALL_SUBFOLDER, MODEL_FILE_NAME);
                Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
                _mlContext.Model.Save(_model, split.TrainSet.Schema, modelPath);
                
                _logger.LogInformation("Rainfall model saved as {FileName}. Metrics: RMSE={RMSE}, MAE={MAE}, R2={R2}", 
                    Path.GetFileName(modelPath), _metrics.RMSE, _metrics.MAE, _metrics.R2);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, "Data file missing");
                throw;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Training data quality issue");
                throw;
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Rainfall model training failed due to file I/O issue");
                throw new InvalidOperationException("Model training failed because training data could not be accessed.", ex);
            }
        }

        /// <summary>
        /// Predicts rainfall based on the provided input data.
        /// </summary>
        public async Task<(RainfallPrediction prediction, RainfallMetrics metrics)> PredictAsync(RainfallData input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            await EnsureModelInitializedAsync();

            if (_model == null || _predictionEngine == null)
            {
                throw new InvalidOperationException("Model is not trained and cannot perform predictions.");
            }

            ValidateInput(input);

            try
            {
                var result = await Task.Run(() => _predictionEngine.Predict(input));

                if (result.PredictedRainfall < 0)
                {
                    result.PredictedRainfall = 0;
                }

                return (result, _metrics);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Prediction failed due to model state issue");
                throw;
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Prediction execution failed due to invalid rainfall input");
                throw new InvalidOperationException("An error occurred while processing the prediction input.", ex);
            }
        }

        private void ValidateInput(RainfallData input)
        {
            if (input.Temperature < -50 || input.Temperature > 60)
            {
                throw new ArgumentOutOfRangeException(nameof(input.Temperature), "Temperature must be between -50 and 60.");
            }

            if (input.Humidity < 0 || input.Humidity > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(input.Humidity), "Humidity must be between 0 and 100.");
            }

            if (input.WindSpeed < 0 || input.WindSpeed > 300)
            {
                throw new ArgumentOutOfRangeException(nameof(input.WindSpeed), "Wind speed must be between 0 and 300.");
            }

            if (input.Pressure < 800 || input.Pressure > 1100)
            {
                throw new ArgumentOutOfRangeException(nameof(input.Pressure), "Pressure must be between 800 and 1100.");
            }
        }

        /// <summary>
        /// Retrieves the evaluation metrics of the trained model.
        /// </summary>
        public RainfallMetrics GetMetrics()
        {
            return _metrics ?? throw new InvalidOperationException("Metrics are not available because the model has not been trained.");
        }
    }
}