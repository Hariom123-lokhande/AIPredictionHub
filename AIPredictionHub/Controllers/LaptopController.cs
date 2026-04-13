using Microsoft.AspNetCore.Mvc;
using AIPredictionHub.Models.Laptop;
using AIPredictionHub.Services;
using Microsoft.AspNetCore.Authorization;

namespace AIPredictionHub.Controllers
{
    [Authorize]
    public class LaptopController : Controller
    {
        private readonly LaptopService _service;
        private readonly ILogger<LaptopController> _logger;

        public LaptopController(LaptopService service, ILogger<LaptopController> logger) //dependency injection
        {
            _service = service;
            _logger = logger;
        }

        // Show form — propagates CsvNotFound flag so the view can render the prompt
        [HttpGet]
        public IActionResult Index()
        {
            return View(new LaptopViewModel
            {
                CsvNotFound = _service.CsvNotFound
            });
        }

        // Called when the user clicks "Yes, use dummy data"
        [HttpPost]
        public IActionResult UseDummyData()
        {
            try
            {
                _logger.LogInformation("Attempting to train Laptop model with dummy data");
                _service.TrainWithDummyData();
                _logger.LogInformation("Laptop model trained successfully with dummy data");
                TempData["DummyDataSuccess"] = "Model trained successfully using dummy data. You can now make predictions.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to train Laptop model with dummy data");
                TempData["DummyDataError"] = $"Failed to train with dummy data: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        // Predict price
        [HttpPost]
        public IActionResult Predict(LaptopViewModel vm)
        {
            // If CSV is still missing, send the user back to the prompt
            if (_service.CsvNotFound)
            {
                _logger.LogWarning("Prediction attempt failed: Training data not available");
                vm.CsvNotFound = true;
                ModelState.AddModelError("", "Training data not available. Please choose to use dummy data first.");
                return View("Index", vm);
            }

            if (!ModelState.IsValid)
            {
                return View("Index", vm);
            }

            try
            {
                var data = new LaptopData
                {
                    Brand     = vm.Input.Brand     ?? "Unknown",
                    RAM       = vm.Input.RAM       ?? 0f,
                    Storage   = vm.Input.Storage   ?? 0f,
                    Processor = vm.Input.Processor ?? "Unknown",
                    ScreenSize = vm.Input.ScreenSize ?? 0f
                };

                _logger.LogInformation("Predicting laptop price for Brand: {Brand}, RAM: {RAM}, Storage: {Storage}", data.Brand, data.RAM, data.Storage);
                var (prediction, metrics) = _service.Predict(data);
                _logger.LogInformation("Laptop price predicted: {Price}", prediction.PredictedPrice);

                vm.Prediction  = prediction;
                vm.Metrics     = metrics;
                vm.IsPredicted = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during laptop price prediction");
                ModelState.AddModelError("", ex.Message);
            }

            return View("Index", vm);
        }

        // Reset form
        [HttpPost]
        public IActionResult Reset()
        {
            return RedirectToAction("Index");
        }
    }
}