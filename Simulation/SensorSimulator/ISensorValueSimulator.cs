using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SensorSimulator
{
    public interface ISensorValueSimulator
    {
        IDictionary<string, SensorValue> Sensors { get; }

        event EventHandler<SensorValue>? SensorValueChanged;

        /// <summary>
        /// Creates the sensors and changes their values every second until <paramref name="cancellationToken"/> is cancelled.
        /// The sensors are created before the method returns; the returned task completes when the simulation has stopped.
        /// </summary>
        Task RunSimulationAsync(int simulationCount, CancellationToken cancellationToken);
    }
}
