using Microsoft.ML;
using Microsoft.ML.Data;
using AIPredictionHub.Models.Laptop;
using Microsoft.Extensions.Logging;

namespace AIPredictionHub.Services
{
    public class LaptopService : ILaptopService
    {
        private const string DATA_FILE_NAME = "laptop.csv";
        private const string MODEL_FILE_NAME = "laptop_model.zip";
        private const string DATA_FOLDER = "Data";
        private const string MODELS_FOLDER = "Models";
        private const string LAPTOP_SUBFOLDER = "Laptop";

        private readonly MLContext _mlContext;
        private readonly ILogger<LaptopService> _logger;
        private readonly Lazy<Task> _initializationTask;
        private ITransformer? _model;
        private PredictionEngine<LaptopData, LaptopPrediction>? _predictionEngine;
        private LaptopMetrics? _metrics;

        /// <summary>True when the CSV file was not found at startup.</summary>
        public bool CsvNotFound { get; private set; } = false;

        public LaptopService(MLContext mlContext, ILogger<LaptopService> logger)
        {
            _mlContext = mlContext;
            _logger = logger;
            _logger.LogInformation("Initializing LaptopService and starting model training");
            _initializationTask = new Lazy<Task>(() => TrainModelAsync());
            _ = _initializationTask.Value;
        }

        private async Task EnsureModelInitializedAsync()
        {
            try
            {
                await _initializationTask.Value;
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Laptop initialization failed due to CSV access issue");
                throw new InvalidOperationException("Laptop model initialization failed because CSV data could not be accessed.", ex);
            }
        }

        /// <summary>
        /// Trains the model using provided fallback dummy data.
        /// </summary>
        public async Task TrainWithDummyDataAsync()
        {
            _logger.LogInformation("Training with dummy data instead of CSV");
            var records = LaptopDummyData.GetRecords();
            var dataView = _mlContext.Data.LoadFromEnumerable(records);
            await BuildAndFitPipelineAsync(dataView);
            CsvNotFound = false; 
            _logger.LogInformation("Laptop model trained successfully with dummy data");
        }

        /// <summary>
        /// Loads and cleans laptop data from the CSV file.
        /// </summary>
        private async Task<IDataView> LoadAndCleanDataAsync(string path)
        {
            _logger.LogInformation("Loading and cleaning data from {FileName}", Path.GetFileName(path));
            
            var rawDataList = await Task.Run(() => _mlContext.Data.LoadFromTextFile<LaptopData>(
                path: path,
                hasHeader: true,
                separatorChar: ','));

            var cleanedList = _mlContext.Data
                .CreateEnumerable<LaptopData>(rawDataList, reuseRowObject: false)
                .Where(data => (data != null) &&
                            (!float.IsNaN(data.RAM)) &&
                            (!float.IsNaN(data.Storage)) &&
                            (!float.IsNaN(data.ScreenSize)) &&
                            (!float.IsNaN(data.Price)))
                .Where(data => (data.RAM > 0 && data.RAM <= 128) &&
                            (data.Storage > 0 && data.Storage <= 4000) &&
                            (data.ScreenSize >= 10 && data.ScreenSize <= 20) &&
                            (data.Price > 0))
                .GroupBy(data => new { data.Brand, data.Processor, data.RAM, data.Storage, data.ScreenSize, data.Price })
                .Select(group => group.First())
                .ToList();

            _logger.LogInformation("Data cleaning complete. {Count} records remain", cleanedList.Count);
            return _mlContext.Data.LoadFromEnumerable(cleanedList);
        }

        /// <summary>
        /// Reads CSV data and trains the model if the file exists.
        /// </summary>
        public async Task TrainModelAsync()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), DATA_FOLDER, DATA_FILE_NAME);

            if (!File.Exists(path))
            {
                _logger.LogWarning("Laptop training data not found: {FileName}", Path.GetFileName(path));
                CsvNotFound = true;
                return;
            }

            try
            {
                var cleanedData = await LoadAndCleanDataAsync(path);
                var count = _mlContext.Data.CreateEnumerable<LaptopData>(cleanedData, false).Count();
                
                if (count < 5)
                {
                    _logger.LogWarning("Not enough valid data after cleaning ({Count} records)", count);
                    throw new InvalidOperationException("Insufficient data for training.");
                }

                await BuildAndFitPipelineAsync(cleanedData);
                _logger.LogInformation("Laptop model successfully trained from CSV");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Laptop training failed due to data quality issue");
                throw;
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Laptop training failed from CSV due to file I/O issue");
                throw new InvalidOperationException("Laptop model training failed because CSV data could not be accessed.", ex);
            }
        }

        /// <summary>
        /// Logic for building the ML pipeline and fitting the model.
        /// </summary>
        private async Task BuildAndFitPipelineAsync(IDataView data)
        {
            _logger.LogInformation("Building ML pipeline and fitting Laptop model");
            var split = _mlContext.Data.TrainTestSplit(data, 0.2);

            var pipeline = _mlContext.Transforms.Categorical.OneHotEncoding("BrandEncoded", nameof(LaptopData.Brand))
                .Append(_mlContext.Transforms.Categorical.OneHotEncoding("ProcessorEncoded", nameof(LaptopData.Processor)))
                .Append(_mlContext.Transforms.Concatenate("Features",
                    "BrandEncoded",
                    "ProcessorEncoded",
                    nameof(LaptopData.RAM),
                    nameof(LaptopData.Storage),
                    nameof(LaptopData.ScreenSize)))
                .Append(_mlContext.Transforms.ReplaceMissingValues("Features"))
                .Append(_mlContext.Transforms.NormalizeMinMax("Features"))
                .Append(_mlContext.Regression.Trainers.LightGbm(
                    labelColumnName: "Label",
                    featureColumnName: "Features"));

            await Task.Run(() => 
            {
                _model = pipeline.Fit(split.TrainSet);
                _predictionEngine = _mlContext.Model.CreatePredictionEngine<LaptopData, LaptopPrediction>(_model);

                var predictions = _model.Transform(split.TestSet);
                var evaluationMetrics = _mlContext.Regression.Evaluate(predictions);

                _metrics = new LaptopMetrics
                {
                    RMSE = Math.Round(evaluationMetrics.RootMeanSquaredError, 3),
                    MAE  = Math.Round(evaluationMetrics.MeanAbsoluteError, 3),
                    R2   = Math.Round(evaluationMetrics.RSquared, 3)
                };
            });

            string modelPath = Path.Combine(Directory.GetCurrentDirectory(), MODELS_FOLDER, LAPTOP_SUBFOLDER, MODEL_FILE_NAME);
            Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
            _mlContext.Model.Save(_model, split.TrainSet.Schema, modelPath);
            _logger.LogInformation("Laptop model saved. Metrics: RMSE={RMSE}, R2={R2}", _metrics?.RMSE, _metrics?.R2);
        }

        /// <summary>
        /// Predicts the price of a laptop based on its specifications.
        /// </summary>
        public async Task<(LaptopPrediction prediction, LaptopMetrics metrics)> PredictAsync(LaptopData input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            await EnsureModelInitializedAsync();

            if (_model == null || _predictionEngine == null)
            {
                throw new InvalidOperationException(CsvNotFound
                    ? "Model is not trained. Please confirm use of dummy data first."
                    : "Model not trained.");
            }

            ValidateInput(input);

            try
            {
                var result = await Task.Run(() => _predictionEngine.Predict(input));

                if (result.PredictedPrice < 0)
                {
                    result.PredictedPrice = 0;
                }

                return (result, _metrics!);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Laptop prediction execution failed due to model state");
                throw;
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Laptop prediction execution failed");
                throw new InvalidOperationException("Prediction failed due to invalid laptop input.", ex);
            }
        }

        private void ValidateInput(LaptopData input)
        {
            if (string.IsNullOrWhiteSpace(input.Brand))
            {
                throw new ArgumentException("Laptop brand is required.", nameof(input.Brand));
            }

            if (string.IsNullOrWhiteSpace(input.Processor))
            {
                throw new ArgumentException("Processor type is required.", nameof(input.Processor));
            }

            if (input.RAM <= 0 || input.RAM > 128)
            {
                throw new ArgumentOutOfRangeException(nameof(input.RAM), "RAM must be between 1 and 128 GB.");
            }

            if (input.Storage <= 0 || input.Storage > 4000)
            {
                throw new ArgumentOutOfRangeException(nameof(input.Storage), "Storage must be between 1 and 4000 GB.");
            }

            if (input.ScreenSize < 10 || input.ScreenSize > 20)
            {
                throw new ArgumentOutOfRangeException(nameof(input.ScreenSize), "Screen size must be between 10 and 20 inches.");
            }
        }

        /// <summary>
        /// Returns the evaluation metrics for the laptop model.
        /// </summary>
        public LaptopMetrics GetMetrics()
        {
            return _metrics ?? throw new InvalidOperationException("Metrics are not available.");
        }
    }
}