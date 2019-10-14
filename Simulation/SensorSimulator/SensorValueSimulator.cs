using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SensorSimulator
{
    public sealed class SensorValueSimulator : ISensorValueSimulator
    {
        public IDictionary<string, SensorValue> Sensors { get; }

        public event EventHandler<SensorValue> SensorValueChanged;

        public SensorValueSimulator()
        {
            Sensors = new Dictionary<string, SensorValue>();
        }

        public void StartSimulation(int simulationCount, CancellationToken cancellationToken)
        {
            for (int i = 0; i < simulationCount; i++)
            {
                var sensorValue = new SensorValue
                    {SensorName = "Sensor" + i, LastChangeDateTime = DateTime.UtcNow.AddHours(-1), Value = i};

                Sensors.Add(sensorValue.SensorName, sensorValue);
            }

            Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    foreach (var sensorValue in Sensors.Values)
                    {
                        sensorValue.Value++;
                        sensorValue.LastChangeDateTime = DateTime.UtcNow.AddHours(-1);

                        OnSensorValueChanged(sensorValue);
                    }

                    await Task.Delay(1000, cancellationToken);
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        private void OnSensorValueChanged(SensorValue e)
        {
            SensorValueChanged?.Invoke(this, e);
        }
    }
}
