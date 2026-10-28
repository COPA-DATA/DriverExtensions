using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SensorSimulator
{
    public sealed class SensorValueSimulator : ISensorValueSimulator
    {
        private static readonly TimeSpan SimulationInterval = TimeSpan.FromSeconds(1);

        private readonly ConcurrentDictionary<string, SensorValue> _sensors;
        private int _started;

        public IDictionary<string, SensorValue> Sensors => _sensors;

        public event EventHandler<SensorValue>? SensorValueChanged;

        public SensorValueSimulator()
        {
            _sensors = new ConcurrentDictionary<string, SensorValue>();
        }

        public async Task RunSimulationAsync(int simulationCount, CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref _started, 1) == 1)
            {
                throw new InvalidOperationException("The simulation has already been started.");
            }

            // NOTE: Simulated timestamps are one hour in the past (UtcNow.AddHours(-1)), here and in the simulation
            // loop below; kept as in the original sample, the reason for the offset is not documented. Values written
            // via the REST simulator's PUT endpoint (SensorsController.Put) are stamped with UtcNow without this
            // offset, so written and simulated values are not consistent.
            for (int i = 0; i < simulationCount; i++)
            {
                var sensorValue = new SensorValue
                    { SensorName = "Sensor" + i, LastChangeDateTime = DateTime.UtcNow.AddHours(-1), Value = i };

                _sensors.TryAdd(sensorValue.SensorName, sensorValue);
            }

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    foreach (var sensorValue in _sensors.Values)
                    {
                        sensorValue.Value++;
                        sensorValue.LastChangeDateTime = DateTime.UtcNow.AddHours(-1);

                        OnSensorValueChanged(sensorValue);
                    }

                    await Task.Delay(SimulationInterval, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }

        private void OnSensorValueChanged(SensorValue e)
        {
            SensorValueChanged?.Invoke(this, e);
        }
    }
}
