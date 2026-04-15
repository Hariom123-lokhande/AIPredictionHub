using Microsoft.AspNetCore.Mvc;
using AIPredictionHub.Models.Fitness;
using AIPredictionHub.Services;
using Microsoft.AspNetCore.Authorization;

namespace AIPredictionHub.Controllers
{
    [Authorize]
    public class FitnessController : Controller
    {
        private readonly IFitnessService _service;
        private readonly ILogger<FitnessController> _logger;

        public FitnessController(IFitnessService service, ILogger<FitnessController> logger) //dependency injection
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// Displays the Fitness prediction entry form.
        /// </summary>
        [HttpGet]
        public IActionResult Index()
        {
            return View(new FitnessViewModel());
        }

        /// <summary>
        /// Processes the calorie burn prediction request.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Predict(FitnessViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                return View("Index", viewModel);
            }

            try
            {
                // Thin controller: map and delegate to service async
                var data = MapToData(viewModel.Input);

                _logger.LogInformation("Predicting calories for input data");
                var (prediction, metrics) = await _service.PredictAsync(data);

                viewModel.Prediction = prediction;
                viewModel.Metrics = metrics;
                viewModel.IsPredicted = true;
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid fitness input received");
                ModelState.AddModelError(string.Empty, "Invalid input values. Please review and try again.");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Error during fitness calorie prediction action");
                ModelState.AddModelError(string.Empty, "Failed to calculate Calories. Please check your inputs.");
            }

            return View("Index", viewModel);
        }

        private FitnessData MapToData(FitnessInputModel input)
        {
            return new FitnessData
            {
                Weight = input.Weight ?? 0f,
                Duration = input.Duration ?? 0f,
                HeartRate = input.HeartRate ?? 0f,
                ExerciseType = input.ExerciseType ?? string.Empty
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