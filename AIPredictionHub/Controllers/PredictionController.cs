using Microsoft.AspNetCore.Mvc;
using AIPredictionHub.Models.Rainfall;
using AIPredictionHub.Models.Fitness;
using AIPredictionHub.Models.Laptop;
using AIPredictionHub.Services;
using AIPredictionHub.DTOs;
namespace AIPredictionHub.Controllers
{
    /// <summary>
    /// Unified API Controller for all ML predictions.
    /// </summary>
    [ApiController]
    [Route("api/prediction")]
    [RequestSizeLimit(10240)] // Limit to 10 KB (Security Standard 17)
    public class PredictionController : ControllerBase
    {
        private readonly IRainfallService _rainfallService;
        private readonly IFitnessService _fitnessService;
        private readonly ILaptopService _laptopService;
        private readonly ILogger<PredictionController> _logger;

        public PredictionController(
            IRainfallService rainfallService,
            IFitnessService fitnessService,
            ILaptopService laptopService,
            ILogger<PredictionController> logger)
        {
            _rainfallService = rainfallService;
            _fitnessService = fitnessService;
            _laptopService = laptopService;
            _logger = logger;
        }

        /// <summary>
        /// Predicts rainfall based on environmental factors.
        /// </summary>
        [HttpPost("rainfall")]
        [RequestSizeLimit(10240)]
        public async Task<IActionResult> PredictRainfall([FromBody] RainfallRequestDto request)
        {
            if (request == null)
            {
                return BadRequest("Request body is empty.");
            }

            try
            {
                // Map DTO to Data
                var data = new RainfallData
                {
                    Temperature = request.Temperature,
                    Humidity = request.Humidity,
                    WindSpeed = request.WindSpeed,
                    Pressure = request.Pressure
                };

                var (prediction, metrics) = await _rainfallService.PredictAsync(data);

                // Map result to Response DTO
                var response = new RainfallResponseDto
                {
                    PredictedRainfall = prediction.PredictedRainfall,
                    RMSE = metrics.RMSE,
                    R2 = metrics.R2
                };

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "API Rainfall request validation failed");
                return BadRequest(new { error = "Invalid rainfall request values." });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "API Rainfall prediction unavailable");
                return StatusCode(503, new { error = "Prediction service is temporarily unavailable." });
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "API Rainfall prediction failed");
                return StatusCode(500, new { error = "An internal error occurred." });
            }
        }

        /// <summary>
        /// Predicts calories burned based on fitness activities.
        /// </summary>
        [HttpPost("fitness")]
        [RequestSizeLimit(10240)]
        public async Task<IActionResult> PredictFitness([FromBody] FitnessRequestDto request)
        {
            if (request == null)
            {
                return BadRequest("Request body is empty.");
            }

            try
            {
                var data = new FitnessData
                {
                    Weight = request.Weight,
                    Duration = request.Duration,
                    HeartRate = request.HeartRate,
                    ExerciseType = request.ExerciseType
                };

                var (prediction, metrics) = await _fitnessService.PredictAsync(data);

                var response = new FitnessResponseDto
                {
                    CaloriesBurned = prediction.CaloriesBurned,
                    RMSE = metrics.RMSE,
                    R2 = metrics.R2
                };

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "API Fitness request validation failed");
                return BadRequest(new { error = "Invalid fitness request values." });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "API Fitness prediction unavailable");
                return StatusCode(503, new { error = "Prediction service is temporarily unavailable." });
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "API Fitness prediction failed");
                return StatusCode(500, new { error = "An internal error occurred." });
            }
        }

        /// <summary>
        /// Predicts laptop price based on hardware specs.
        /// </summary>
        [HttpPost("laptop")]
        [RequestSizeLimit(10240)]
        public async Task<IActionResult> PredictLaptop([FromBody] LaptopRequestDto request)
        {
            if (request == null)
            {
                return BadRequest("Request body is empty.");
            }

            try
            {
                var data = new LaptopData
                {
                    Brand = request.Brand,
                    Processor = request.Processor,
                    RAM = request.RAM,
                    Storage = request.Storage,
                    ScreenSize = request.ScreenSize
                };

                var (prediction, metrics) = await _laptopService.PredictAsync(data);

                var response = new LaptopResponseDto
                {
                    PredictedPrice = prediction.PredictedPrice,
                    RMSE = metrics.RMSE,
                    R2 = metrics.R2
                };

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "API Laptop request validation failed");
                return BadRequest(new { error = "Invalid laptop request values." });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "API Laptop prediction unavailable");
                return StatusCode(503, new { error = "Prediction service is temporarily unavailable." });
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "API Laptop prediction failed");
                return StatusCode(500, new { error = "An internal error occurred." });
            }
        }
    }
}
