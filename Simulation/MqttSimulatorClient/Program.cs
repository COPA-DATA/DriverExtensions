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



        Logger.Info("Starting simulation");

        var cancellationTokenSource = new CancellationTokenSource();

        var publisher = new Publisher();
        await publisher.ConnectAsync();

        await publisher.StartSimulation(simulationCount, cancellationTokenSource.Token);

        Logger.Info("Simulation started. Press key to stop simulation.");
        Console.ReadKey();

        Logger.Info("Simulation stopping...");
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Token.WaitHandle.WaitOne();

        Console.WriteLine("Simulation stopped");
      }
      catch (Exception e)
      {
        Logger.Error(e);
      }


    }
  }
}
