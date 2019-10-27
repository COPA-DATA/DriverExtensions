using System;

namespace MqttSimulatorClient
{
  public class SensorPayload
  {
    public double Value { get; set; }

    public DateTime LastChangeDateTime { get; set; }
  }
}
