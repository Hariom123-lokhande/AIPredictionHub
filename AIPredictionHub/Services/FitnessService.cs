using Microsoft.ML;
using Microsoft.ML.Data;
using AIPredictionHub.Models.Fitness;
using Microsoft.Extensions.Logging;

namespace AIPredictionHub.Services
{
    public class FitnessService : IFitnessService
    {
        private const string DATA_FILE_NAME = "fitness.csv";
        private const string MODEL_FILE_NAME = "fitness_model.zip";
        private const string DATA_FOLDER = "Data";
        private const string MODELS_FOLDER = "Models";
        private const string FITNESS_SUBFOLDER = "Fitness";

        private readonly MLContext _mlContext;
        private readonly ILogger<FitnessService> _logger;
        private readonly Lazy<Task> _initializationTask;
        private ITransformer _model = null!;
        private PredictionEngine<FitnessData, FitnessPrediction> _predictionEngine = null!;
        private FitnessMetrics _metrics = null!;

        public FitnessService(MLContext mlContext, ILogger<FitnessService> logger)
        {
            _mlContext = mlContext;
            _logger = logger;
            _logger.LogInformation("Initializing FitnessService and training model");
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
                _logger.LogError(ex, "Fitness initialization failed due to missing data file");
                throw new InvalidOperationException("Fitness model initialization failed because training data is missing.", ex);
            }
        }

        /// <summary>
        /// Loads fitness data from a CSV file and performs cleaning.
        /// </summary>
        private async Task<IDataView> LoadAndCleanDataAsync(string path)
        {
            _logger.LogInformation("Loading and cleaning data from {FileName}", Path.GetFileName(path));
            
            var rawDataList = await Task.Run(() => _mlContext.Data.LoadFromTextFile<FitnessData>(
                path: path,
                hasHeader: true,
                separatorChar: ','));

            var cleanedList = _mlContext.Data
                .CreateEnumerable<FitnessData>(rawDataList, reuseRowObject: false)
                .Where(data => (data != null) &&
                            (!float.IsNaN(data.Weight)) &&
                            (!float.IsNaN(data.Duration)) &&
                            (!float.IsNaN(data.HeartRate)) &&
                            (!string.IsNullOrWhiteSpace(data.ExerciseType)) &&
                            (!float.IsNaN(data.CaloriesBurned)))
                .Where(data => (data.Weight >= 20 && data.Weight <= 300) &&
                            (data.Duration >= 1 && data.Duration <= 300) &&
                            (data.HeartRate >= 40 && data.HeartRate <= 220) &&
                            (data.CaloriesBurned >= 0))
                .GroupBy(data => new { data.Weight, data.Duration, data.HeartRate, data.ExerciseType, data.CaloriesBurned })
                .Select(group => group.First())
                .ToList();

            _logger.LogInformation("Data cleaning complete. {Count} records remain", cleanedList.Count);
            return _mlContext.Data.LoadFromEnumerable(cleanedList);
        }

        /// <summary>
        /// Orchestrates the training process for the Fitness model.
        /// </summary>
        public async Task TrainModelAsync()
        {
            try
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(), DATA_FOLDER, DATA_FILE_NAME);

                if (!File.Exists(path))
                {
                    _logger.LogError("Fitness training data not found: {FileName}", Path.GetFileName(path));
                    throw new FileNotFoundException("Fitness training data not found.", Path.GetFileName(path));
                }

                var cleanedData = await LoadAndCleanDataAsync(path);
                var count = _mlContext.Data.CreateEnumerable<FitnessData>(cleanedData, false).Count();
                
                if (count < 5)
                {
                    _logger.LogWarning("Not enough valid data after cleaning ({Count} records)", count);
                    throw new InvalidOperationException("Not enough valid data for training.");
                }

                _logger.LogInformation("Building ML pipeline and fitting Fitness model");
                var split = _mlContext.Data.TrainTestSplit(cleanedData, testFraction: 0.2);

                var pipeline = _mlContext.Transforms
                    .Categorical.OneHotEncoding("ExerciseTypeEncoded", nameof(FitnessData.ExerciseType))
                    .Append(_mlContext.Transforms.Concatenate("Features",
                        nameof(FitnessData.Weight),
                        nameof(FitnessData.Duration),
                        nameof(FitnessData.HeartRate),
                        "ExerciseTypeEncoded"))
                    .Append(_mlContext.Transforms.ReplaceMissingValues("Features"))
                    .Append(_mlContext.Transforms.NormalizeMinMax("Features"))
                    .Append(_mlContext.Regression.Trainers.LightGbm(
                        labelColumnName: "Label",
                        featureColumnName: "Features"));

                await Task.Run(() => 
                {
                    _model = pipeline.Fit(split.TrainSet);
                    _predictionEngine = _mlContext.Model.CreatePredictionEngine<FitnessData, FitnessPrediction>(_model);

                    var predictions = _model.Transform(split.TestSet);
                    var evaluationMetrics = _mlContext.Regression.Evaluate(predictions);

                    _metrics = new FitnessMetrics
                    {
                        RMSE = Math.Round(evaluationMetrics.RootMeanSquaredError, 3),
                        MAE = Math.Round(evaluationMetrics.MeanAbsoluteError, 3),
                        R2 = Math.Round(evaluationMetrics.RSquared, 3)
                    };
                });

                string modelPath = Path.Combine(Directory.GetCurrentDirectory(), MODELS_FOLDER, FITNESS_SUBFOLDER, MODEL_FILE_NAME);
                Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
                _mlContext.Model.Save(_model, split.TrainSet.Schema, modelPath);
                
                _logger.LogInformation("Fitness model saved metrics: RMSE={RMSE}, MAE={MAE}, R2={R2}", 
                    _metrics.RMSE, _metrics.MAE, _metrics.R2);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, "Missing fitness data file");
                throw;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Fitness training data quality issue");
                throw;
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Fitness model training failed due to file I/O issue");
                throw new InvalidOperationException("Model training failed because training data could not be accessed.", ex);
            }
        }

        /// <summary>
        /// Predicts calories burned based on user fitness data.
        /// </summary>
        public async Task<(FitnessPrediction prediction, FitnessMetrics metrics)> PredictAsync(FitnessData input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            await EnsureModelInitializedAsync();

            if (_model == null || _predictionEngine == null)
            {
                throw new InvalidOperationException("Fitness model is not trained.");
            }

            ValidateInput(input);

            try
            {
                var result = await Task.Run(() => _predictionEngine.Predict(input));

                if (result.CaloriesBurned < 0)
                {
                    result.CaloriesBurned = 0;
                }

                return (result, _metrics);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Fitness prediction failed due to model state");
                throw;
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Fitness prediction failed due to invalid input");
                throw new InvalidOperationException("Prediction execution failed due to invalid input.", ex);
            }
        }

        private void ValidateInput(FitnessData input)
        {
            if (input.Weight < 20 || input.Weight > 300)
            {
                throw new ArgumentOutOfRangeException(nameof(input.Weight), "Weight must be between 20 and 300.");
            }
            if (input.Duration <= 0 || input.Duration > 300)
            {
                throw new ArgumentOutOfRangeException(nameof(input.Duration), "Duration must be between 1 and 300.");
            }
            if (input.HeartRate < 40 || input.HeartRate > 220)
            {
                throw new ArgumentOutOfRangeException(nameof(input.HeartRate), "Heart rate must be between 40 and 220.");
            }
            if (string.IsNullOrWhiteSpace(input.ExerciseType))
            {
                throw new ArgumentException("Exercise type is required.", nameof(input.ExerciseType));
            }
        }

        /// <summary>
        /// Gets model version evaluation metrics.
        /// </summary>
        public FitnessMetrics GetMetrics()
        {
            return _metrics ?? throw new InvalidOperationException("Metrics unavailable.");
        }
    }
}