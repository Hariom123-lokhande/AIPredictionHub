using Microsoft.AspNetCore.Mvc;
using AIPredictionHub.Models.Rainfall;
using AIPredictionHub.Services;
using Microsoft.AspNetCore.Authorization;

namespace AIPredictionHub.Controllers
{
    [Authorize]
    public class RainfallController : Controller
    {
        private readonly IRainfallService _service;
        private readonly ILogger<RainfallController> _logger;

        public RainfallController(IRainfallService service, ILogger<RainfallController> logger) //dependency injection
        {
            _service = service;
            _logger = logger;
        }

        // get: Show Form
        [HttpGet]
        public IActionResult Index()
        {
            var viewModel = new RainfallViewModel();
            return View(viewModel);
        }

        /// <summary>
        /// Handles the rainfall prediction request.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Predict(RainfallViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                viewModel.IsPredicted = false;
                return View("Index", viewModel);
            }

            try
            {
                // Mapping VM to Data logic (Standard 22.1: Keep controllers thin)
                var rainfallData = MapToData(viewModel.Input);

                _logger.LogInformation("Predicting rainfall for input data");
                
                // Call Service Async
                var (prediction, metrics) = await _service.PredictAsync(rainfallData);
                
                viewModel.Prediction = prediction;
                viewModel.Metrics = metrics;
                viewModel.IsPredicted = true;
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid rainfall input received");
                ModelState.AddModelError(string.Empty, "Invalid input values. Please review and try again.");
                viewModel.IsPredicted = false;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Error during rainfall prediction action");
                ModelState.AddModelError(string.Empty, "An error occurred while calculating the prediction. Please try again.");
                viewModel.IsPredicted = false;
            }

            return View("Index", viewModel);
        }

        private RainfallData MapToData(RainfallInputModel input)
        {
            return new RainfallData
            {
                Temperature = input.Temperature ?? 0f,
                Humidity = input.Humidity ?? 0f,
                WindSpeed = input.WindSpeed ?? 0f,
                Pressure = input.Pressure ?? 0f
            };
        }

        //reset button
        [HttpPost]
        public IActionResult Reset()
        {
            return RedirectToAction("Index");
        }
    }
}