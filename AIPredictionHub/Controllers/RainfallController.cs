using Microsoft.AspNetCore.Mvc;
using AIPredictionHub.Models.Rainfall;
using AIPredictionHub.Services;
using Microsoft.AspNetCore.Authorization;

namespace AIPredictionHub.Controllers
{
    [Authorize]
    public class RainfallController : Controller
    {
        private readonly RainfallService _service;
        private readonly ILogger<RainfallController> _logger;

        public RainfallController(RainfallService service, ILogger<RainfallController> logger) //dependency injection
        {
            _service = service;
            _logger = logger;
        }

        // get: Show Form
        [HttpGet]
        public IActionResult Index()
        {
            var vm = new RainfallViewModel();
            return View(vm);
        }
        //post predict
       
        [HttpPost]
        public IActionResult Predict(RainfallViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                vm.IsPredicted = false;
                return View("Index", vm);
            }

            try
            {
                // Map InputModel to RainfallData
                var rainfallData = new RainfallData
                {
                    Temperature = vm.Input.Temperature ?? 0f,
                    Humidity = vm.Input.Humidity ?? 0f,
                    WindSpeed = vm.Input.WindSpeed ?? 0f,
                    Pressure = vm.Input.Pressure ?? 0f
                };

                _logger.LogInformation("Predicting rainfall for Temperature: {Temperature}, Humidity: {Humidity}, WindSpeed: {WindSpeed}, Pressure: {Pressure}", rainfallData.Temperature, rainfallData.Humidity, rainfallData.WindSpeed, rainfallData.Pressure);
                // Call Service
                var (prediction, metrics) = _service.Predict(rainfallData);
                _logger.LogInformation("Rainfall predicted: {Rainfall}", prediction.PredictedRainfall);

                vm.Prediction = prediction;
                vm.Metrics = metrics;
                vm.IsPredicted = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during rainfall prediction");
                ModelState.AddModelError("", ex.Message);
                vm.IsPredicted = false;
            }

            return View("Index", vm);
        }

        //reset button
        [HttpPost]
        public IActionResult Reset()
        {
            return RedirectToAction("Index");
        }
    }
}