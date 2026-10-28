using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NLog;

namespace MqttSimulatorClient
{
    class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        static async Task Main(string[] args)
        {
            try
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .Build();

                var simulationCount = Convert.ToInt32(config["SimulationCount"]);
                var serverAddress = config["MqttServerAddress"] ?? "127.0.0.1";
                var serverPort = Convert.ToInt32(config["MqttServerPort"] ?? "1883");

                Logger.Info("Starting simulation");

                using var cancellationTokenSource = new CancellationTokenSource();

                var publisher = new Publisher();
                await publisher.ConnectAsync(serverAddress, serverPort);

                var simulation = publisher.RunSimulationAsync(simulationCount, cancellationTokenSource.Token);

                Logger.Info("Simulation started. Press key to stop simulation.");
                Console.ReadKey();

                Logger.Info("Simulation stopping...");
                cancellationTokenSource.Cancel();
                await simulation;
                await publisher.DisconnectAsync();

                Console.WriteLine("Simulation stopped");
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }
    }
}
