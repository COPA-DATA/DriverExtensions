using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SensorSimulator;

namespace RestSimulatorService
{
    /// <summary>
    /// Runs the sensor simulation for the lifetime of the host.
    /// </summary>
    public class SimulationService : BackgroundService
    {
        private readonly ISensorValueSimulator _sensorValueSimulator;
        private readonly int _simulationCount;

        public SimulationService(ISensorValueSimulator sensorValueSimulator, IConfiguration configuration)
        {
            _sensorValueSimulator = sensorValueSimulator;
            _simulationCount = Convert.ToInt32(configuration["SimulationCount"]);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return _sensorValueSimulator.RunSimulationAsync(_simulationCount, stoppingToken);
        }
    }
}
