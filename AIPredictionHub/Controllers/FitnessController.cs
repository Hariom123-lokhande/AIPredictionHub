using Microsoft.AspNetCore.Mvc;
using AIPredictionHub.Models.Fitness;
using AIPredictionHub.Services;
using Microsoft.AspNetCore.Authorization;

namespace AIPredictionHub.Controllers
{
    [Authorize]
    public class FitnessController : Controller
    {
        private readonly FitnessService _service;
        private readonly ILogger<FitnessController> _logger;

        public FitnessController(FitnessService service, ILogger<FitnessController> logger) //dependency injection
        {
            _service = service;
            _logger = logger;
        }

        // Show form
        [HttpGet]
        public IActionResult Index()
        {
            return View(new FitnessViewModel());
        }

        // Predict calories
        [HttpPost]
        public IActionResult Predict(FitnessViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View("Index", vm);
            }

            try
            {
                var data = new FitnessData
                {
                    Weight = vm.Input.Weight ?? 0f,
                    Duration = vm.Input.Duration ?? 0f,
                    HeartRate = vm.Input.HeartRate ?? 0f,
                    ExerciseType = vm.Input.ExerciseType
                };

                _logger.LogInformation("Predicting calories for Weight: {Weight}, Duration: {Duration}, HeartRate: {HeartRate}, ExerciseType: {ExerciseType}", data.Weight, data.Duration, data.HeartRate, data.ExerciseType);
                var (prediction, metrics) = _service.Predict(data);
                _logger.LogInformation("Calories predicted: {Calories}", prediction.CaloriesBurned);

                vm.Prediction = prediction;
                vm.Metrics = metrics;
                vm.IsPredicted = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during fitness calorie prediction");
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