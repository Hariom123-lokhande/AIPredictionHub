using Microsoft.ML;
using Microsoft.ML.Data;
using AIPredictionHub.Models.Laptop;
using Microsoft.Extensions.Logging;

namespace AIPredictionHub.Services
{
    public class LaptopService
    {
        private readonly MLContext _mlContext;
        private readonly ILogger<LaptopService> _logger;
        private ITransformer? _model;
        private PredictionEngine<LaptopData, LaptopPrediction>? _predictionEngine;
        private LaptopMetrics? _metrics;

        /// <summary>True when the CSV file was not found at startup.</summary>
        public bool CsvNotFound { get; private set; } = false;

        public LaptopService(MLContext mlContext, ILogger<LaptopService> logger) //dependency injection
        {
            _mlContext = mlContext;
            _logger = logger;
            _logger.LogInformation("Initializing LaptopService and starting model training");
            TrainModel();
        }
        /// Trains the model using the static <see cref="LaptopDummyData"/> records.
        public void TrainWithDummyData()
        {
            _logger.LogInformation("Training with dummy data instead of CSV");
            var records = LaptopDummyData.GetRecords();
            var dataView = _mlContext.Data.LoadFromEnumerable(records);
            BuildAndFitPipeline(dataView);
            CsvNotFound = false;   // model is now ready
            _logger.LogInformation("Laptop model trained successfully with dummy data");
        }
        // DATA CLEANING

        private IDataView LoadAndCleanData(string path)
        {
            _logger.LogInformation("Loading and cleaning data from {FileName}", Path.GetFileName(path));
            var rawData = _mlContext.Data.LoadFromTextFile<LaptopData>(
                path: path,
                hasHeader: true,
                separatorChar: ',');

            var cleanedList = _mlContext.Data
                .CreateEnumerable<LaptopData>(rawData, reuseRowObject: false)

                // Remove rows with missing values
                .Where(x =>
                    x != null &&
                    !float.IsNaN(x.RAM) &&
                    !float.IsNaN(x.Storage) &&
                    !float.IsNaN(x.ScreenSize) &&
                    !float.IsNaN(x.Price))

                // Remove unrealistic values
                .Where(x =>
                    x.RAM > 0 && x.RAM <= 128 &&
                    x.Storage > 0 && x.Storage <= 4000 &&
                    x.ScreenSize >= 10 && x.ScreenSize <= 20 &&
                    x.Price > 0)

                // Remove duplicate rows
                .GroupBy(x => new
                {
                    x.Brand,
                    x.Processor,
                    x.RAM,
                    x.Storage,
                    x.ScreenSize,
                    x.Price
                })
                .Select(g => g.First())

                .ToList();

            _logger.LogInformation("Data cleaning complete. {Count} records remain", cleanedList.Count);
            return _mlContext.Data.LoadFromEnumerable(cleanedList);
        }
        // MODEL TRAINING
        private void TrainModel()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Data", "laptop.csv");

            // If the CSV is missing, set the flag and return — do NOT throw.
            if (!File.Exists(path))
            {
                _logger.LogWarning("Laptop training data not found: {FileName}", Path.GetFileName(path));
                CsvNotFound = true;
                return;
            }

            try
            {
                // Load and clean data
                var cleanedData = LoadAndCleanData(path);

                // Ensure enough data for training
                var count = _mlContext.Data.CreateEnumerable<LaptopData>(cleanedData, false).Count();
                if (count < 5)
                {
                    _logger.LogWarning("Not enough valid data after cleaning ({Count} records)", count);
                    throw new Exception("Not enough valid data after cleaning.");
                }

                BuildAndFitPipeline(cleanedData);
                _logger.LogInformation("Laptop model successfully trained from CSV");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Laptop training failed from CSV");
                throw new Exception($"Training failed: {ex.Message}");
            }
        }

        private void BuildAndFitPipeline(IDataView data)
        {
            _logger.LogInformation("Building ML pipeline and fitting model");
            // Split data into train and test
            var split = _mlContext.Data.TrainTestSplit(data, 0.2);

            // Build ML pipeline
            var pipeline = _mlContext.Transforms.Categorical.OneHotEncoding("BrandEncoded", nameof(LaptopData.Brand))
                .Append(_mlContext.Transforms.Categorical.OneHotEncoding("ProcessorEncoded", nameof(LaptopData.Processor)))
                .Append(_mlContext.Transforms.Concatenate("Features",
                    "BrandEncoded",
                    "ProcessorEncoded",
                    nameof(LaptopData.RAM),
                    nameof(LaptopData.Storage),
                    nameof(LaptopData.ScreenSize)))

                // Handle missing values
                .Append(_mlContext.Transforms.ReplaceMissingValues("Features"))

                // Normalize feature values
                .Append(_mlContext.Transforms.NormalizeMinMax("Features"))

                // Train using LightGBM regression
                .Append(_mlContext.Regression.Trainers.LightGbm(
                    labelColumnName: "Label",
                    featureColumnName: "Features"));

            // Train model
            _model = pipeline.Fit(split.TrainSet);

            //Create prediction engine
            _predictionEngine = _mlContext.Model.CreatePredictionEngine<LaptopData, LaptopPrediction>(_model);

            // Evaluate model performance
            var predictions = _model.Transform(split.TestSet);
            var metrics = _mlContext.Regression.Evaluate(predictions);

            // Save trained model
            string modelPath = Path.Combine(Directory.GetCurrentDirectory(), "Models", "Laptop", "laptop_model.zip");
            Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
            _mlContext.Model.Save(_model, split.TrainSet.Schema, modelPath);
            _logger.LogInformation("Laptop model saved as {FileName}", Path.GetFileName(modelPath));

            // Store evaluation metrics
            _metrics = new LaptopMetrics
            {
                RMSE = Math.Round(metrics.RootMeanSquaredError, 3),
                MAE  = Math.Round(metrics.MeanAbsoluteError, 3),
                R2   = Math.Round(metrics.RSquared, 3)
            };
            _logger.LogInformation("Laptop model metrics: RMSE={RMSE}, MAE={MAE}, R2={R2}", _metrics.RMSE, _metrics.MAE, _metrics.R2);
        }
        // PREDICTION
        public (LaptopPrediction prediction, LaptopMetrics metrics) Predict(LaptopData input)
        {
            if (input == null)
            {
                _logger.LogWarning("Prediction failed: Input was null");
                throw new ArgumentNullException(nameof(input));
            }

            if (_model == null || _predictionEngine == null)
            {
                _logger.LogWarning("Prediction failed: Model or PredictionEngine is null");
                throw new Exception(CsvNotFound
                    ? "Model is not trained. Please confirm use of dummy data first."
                    : "Model not trained.");
            }

            // Validate input data
            ValidateInput(input);

            try
            {
                _logger.LogDebug("Running prediction for input: {@Input}", input);
                var result = _predictionEngine.Predict(input);

                // Ensure prediction is not negative
                if (result.PredictedPrice < 0)
                {
                    result.PredictedPrice = 0;
                }

                return (result, _metrics!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Prediction execution failed");
                throw new Exception($"Prediction failed: {ex.Message}");
            }
        }
        //input validation
        private void ValidateInput(LaptopData input)
        {
            if (string.IsNullOrWhiteSpace(input.Brand))
            {
                throw new ArgumentException("Missing Brand");
            }

            if (string.IsNullOrWhiteSpace(input.Processor))
            {
                throw new ArgumentException("Missing Processor");
            }

            if (input.RAM <= 0 || input.RAM > 128)
            {
                throw new ArgumentException("Invalid RAM");
            }

            if (input.Storage <= 0 || input.Storage > 4000)
            {
                throw new ArgumentException("Invalid Storage");
            }

            if (input.ScreenSize < 10 || input.ScreenSize > 20)
            {
                throw new ArgumentException("Invalid Screen Size");
            }
        }
        //Model evaluation metrics
        public LaptopMetrics GetMetrics()
        {
            if (_metrics == null)
            {
                throw new Exception("Metrics not available");
            }

            return _metrics;
        }
    }
}