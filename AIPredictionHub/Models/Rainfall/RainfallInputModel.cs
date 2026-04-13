using System.ComponentModel.DataAnnotations;

namespace AIPredictionHub.Models.Rainfall
{
    public class RainfallInputModel
    {
        [Required]
        [Range(-50, 60, ErrorMessage = "Temperature must be between -50°C and 60°C")]
        public float? Temperature { get; set; }

        [Required]
        [Range(0, 100, ErrorMessage = "Humidity must be between 0% and 100%")]
        public float? Humidity { get; set; }

        [Required]
        [Range(0, 300, ErrorMessage = "Wind Speed must be between 0 and 300 km/h")]
        public float? WindSpeed { get; set; }

        [Required]
        [Range(800, 1100, ErrorMessage = "Pressure must be between 800 and 1100 hPa")]
        public float? Pressure { get; set; }
    }
}
