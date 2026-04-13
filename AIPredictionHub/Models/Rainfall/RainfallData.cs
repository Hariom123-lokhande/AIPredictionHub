using Microsoft.ML.Data;
using System.ComponentModel.DataAnnotations;

namespace AIPredictionHub.Models.Rainfall
{
    public class RainfallData
    {
        [LoadColumn(0)]
        [Required]
        [Range(-50, 60, ErrorMessage = "Temperature must be between -50°C and 60°C")]
        public float Temperature { get; set; }

        [LoadColumn(1)]
        [Required]
        [Range(0, 100, ErrorMessage = "Humidity must be between 0% and 100%")]
        public float Humidity { get; set; }

        [LoadColumn(2)]
        [Required]
        [Range(0, 300, ErrorMessage = "Wind Speed must be between 0 and 300 km/h")]
        public float WindSpeed { get; set; }

        [LoadColumn(3)]
        [Required]
        [Range(800, 1100, ErrorMessage = "Pressure must be between 800 and 1100 hPa")]
        public float Pressure { get; set; }

        [LoadColumn(4)]
        [ColumnName("Label")]
        public float Rainfall { get; set; }
    }
}
