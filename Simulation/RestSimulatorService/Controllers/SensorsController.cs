using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SensorSimulator;

namespace RestSimulatorService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SensorsController : ControllerBase
    {
        private readonly ILogger<SensorsController> _logger;
        private readonly ISensorValueSimulator _sensorValueSimulator;

        public SensorsController(ILogger<SensorsController> logger, ISensorValueSimulator sensorValueSimulator)
        {
            _logger = logger;
            _sensorValueSimulator = sensorValueSimulator;
        }

        [HttpGet]
        public IActionResult Get([Required] string sensorName)
        {
            if (string.IsNullOrWhiteSpace(sensorName))
            {
                return BadRequest();
            }

            if (_sensorValueSimulator.Sensors.TryGetValue(sensorName, out SensorValue sensorValue))
            {
                return Ok(sensorValue);
            }

            return NotFound();
        }


        [HttpPut]
        public IActionResult Put([Required] string sensorName, [Required] double value)
        {
            if (string.IsNullOrWhiteSpace(sensorName))
            {
                return BadRequest();
            }

            if (_sensorValueSimulator.Sensors.TryGetValue(sensorName, out SensorValue sensorValue))
            {
                sensorValue.Value = value;
                sensorValue.LastChangeDateTime = DateTime.UtcNow;
                return Ok();
            }

            return NotFound();
        }
    }
}
