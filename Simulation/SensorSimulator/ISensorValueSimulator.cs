using System;
using System.Collections.Generic;
using System.Threading;

namespace SensorSimulator
{
    public interface ISensorValueSimulator
    {
        IDictionary<string, SensorValue> Sensors { get; }

        event EventHandler<SensorValue> SensorValueChanged;
        void StartSimulation(int simulationCount, CancellationToken cancellationToken);
    }
}