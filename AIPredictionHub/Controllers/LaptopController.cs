using Microsoft.AspNetCore.Mvc;
using AIPredictionHub.Models.Laptop;
using AIPredictionHub.Services;
using Microsoft.AspNetCore.Authorization;

namespace AIPredictionHub.Controllers
{
    [Authorize]
    public class LaptopController : Controller
    {
        private readonly ILaptopService _service;
        private readonly ILogger<LaptopController> _logger;

        public LaptopController(ILaptopService service, ILogger<LaptopController> logger) //dependency injection
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// Displays the Laptop price prediction entry form.
        /// </summary>
        [HttpGet]
        public IActionResult Index()
        {
            return View(new LaptopViewModel
            {
                CsvNotFound = _service.CsvNotFound
            });
        }

        /// <summary>
        /// Trains the model using dummy data when CSV is missing.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UseDummyData()
        {
            try
            {
                _logger.LogInformation("Attempting to train Laptop model with dummy data");
                await _service.TrainWithDummyDataAsync();
                TempData["DummyDataSuccess"] = "Model trained successfully using dummy data. You can now make predictions.";
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Laptop model training operation failed");
                TempData["DummyDataError"] = "Training failed because the model pipeline could not be initialized.";
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Failed to train Laptop model with dummy data");
                TempData["DummyDataError"] = "Failed to train with dummy data. Please try again later.";
            }

            return RedirectToAction("Index");
        }

        /// <summary>
        /// Processes the laptop price prediction request.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Predict(LaptopViewModel viewModel)
        {
            if (_service.CsvNotFound)
            {
                viewModel.CsvNotFound = true;
                ModelState.AddModelError(string.Empty, "Training data not available. Please choose to use dummy data first.");
                return View("Index", viewModel);
            }

            if (!ModelState.IsValid)
            {
                return View("Index", viewModel);
            }

            try
            {
                // Thin controller mapping
                var data = MapToData(viewModel.Input);

                _logger.LogInformation("Predicting laptop price for input data");
                var (prediction, metrics) = await _service.PredictAsync(data);

                viewModel.Prediction  = prediction;
                viewModel.Metrics     = metrics;
                viewModel.IsPredicted = true;
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid laptop input received");
                ModelState.AddModelError(string.Empty, "Invalid laptop details. Please review the inputs.");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Error during laptop price prediction action");
                ModelState.AddModelError(string.Empty, "Could not calculate prediction. Check if inputs are valid.");
            }

            return View("Index", viewModel);
        }

        private LaptopData MapToData(LaptopInputModel input)
        {
            return new LaptopData
            {
                Brand     = input.Brand     ?? string.Empty,
                RAM       = input.RAM       ?? 0f,
                Storage   = input.Storage   ?? 0f,
                Processor = input.Processor ?? string.Empty,
                ScreenSize = input.ScreenSize ?? 0f
            };
        }

        // Reset form
        [HttpPost]
        public IActionResult Reset()
        {
            return RedirectToAction("Index");
        }
    }
}