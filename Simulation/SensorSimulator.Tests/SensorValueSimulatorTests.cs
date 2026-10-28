using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SensorSimulator;
using Xunit;

namespace SensorSimulator.Tests
{
    public class SensorValueSimulatorTests
    {
        [Fact]
        public async Task RunSimulationAsync_CreatesSensorsBeforeReturning()
        {
            var simulator = new SensorValueSimulator();
            using var cancellationTokenSource = new CancellationTokenSource();

            var simulation = simulator.RunSimulationAsync(3, cancellationTokenSource.Token);

            Assert.Equal(new[] { "Sensor0", "Sensor1", "Sensor2" }, simulator.Sensors.Keys.OrderBy(k => k));

            cancellationTokenSource.Cancel();
            await simulation;
        }

        [Fact]
        public async Task RunSimulationAsync_IncrementsValuesAndRaisesEvents()
        {
            var simulator = new SensorValueSimulator();
            var changed = new ConcurrentBag<string>();
            simulator.SensorValueChanged += (_, value) => changed.Add(value.SensorName);
            using var cancellationTokenSource = new CancellationTokenSource();

            var simulation = simulator.RunSimulationAsync(2, cancellationTokenSource.Token);

            Assert.Equal(1, simulator.Sensors["Sensor0"].Value);
            Assert.Equal(2, simulator.Sensors["Sensor1"].Value);
            Assert.Equal(new[] { "Sensor0", "Sensor1" }, changed.OrderBy(n => n));

            cancellationTokenSource.Cancel();
            await simulation;
        }

        [Fact]
        public async Task RunSimulationAsync_CompletesWithoutExceptionWhenCancelled()
        {
            var simulator = new SensorValueSimulator();
            using var cancellationTokenSource = new CancellationTokenSource();

            var simulation = simulator.RunSimulationAsync(1, cancellationTokenSource.Token);
            cancellationTokenSource.Cancel();

            await simulation.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(simulation.IsCompletedSuccessfully);
        }

        [Fact]
        public async Task RunSimulationAsync_ThrowsWhenStartedTwice()
        {
            var simulator = new SensorValueSimulator();
            using var cancellationTokenSource = new CancellationTokenSource();

            var simulation = simulator.RunSimulationAsync(1, cancellationTokenSource.Token);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => simulator.RunSimulationAsync(1, cancellationTokenSource.Token));

            cancellationTokenSource.Cancel();
            await simulation;
        }
    }
}
