using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using Newtonsoft.Json;
using NLog;
using SensorSimulator;

namespace MqttSimulatorClient
{
    class Publisher
    {
        private readonly IMqttClient _mqttClient;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly SensorValueSimulator _sensorValueSimulator;

        public Publisher()
        {
            _sensorValueSimulator = new SensorValueSimulator();
            _sensorValueSimulator.SensorValueChanged += async (sender, value) => await SensorValueChanged(sender, value);
            var factory = new MqttFactory();

            _mqttClient = factory.CreateMqttClient();
        }

        public async Task ConnectAsync()
        {
            // Create TCP based options using the builder.
            var options = new MqttClientOptionsBuilder()
                .WithClientId("producer_{268EEB35-2130-49AA-A757-E4D2B05B5D71}")
                .WithTcpServer("127.0.0.1", 1883)
                .WithCleanSession()
                .Build();

            await _mqttClient.ConnectAsync(options, CancellationToken.None);

            _mqttClient.DisconnectedAsync += async e =>
            {
                Logger.Warn("### DISCONNECTED FROM SERVER ###");
                await Task.Delay(TimeSpan.FromSeconds(5));

                try
                {
                    await _mqttClient.ReconnectAsync();

                    foreach (var sensorValue in _sensorValueSimulator.Sensors.Values)
                    {
                        await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(sensorValue.SensorName).Build());
                    }
                }
                catch
                {
                    Logger.Error("### RECONNECTING FAILED ###");
                }
            };

            _mqttClient.ApplicationMessageReceivedAsync += args =>
            {
                var topic = args.ApplicationMessage.Topic;

                var payloadString = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment.Array);
                var sensorPayload = JsonConvert.DeserializeObject<SensorPayload>(payloadString);
                _sensorValueSimulator.Sensors[topic].Value = sensorPayload.Value;
                _sensorValueSimulator.Sensors[topic].LastChangeDateTime = sensorPayload.LastChangeDateTime;
                return Task.CompletedTask;
            };
        }


        private async Task SensorValueChanged(object sender, SensorValue sensorValue)
        {
            var payload = JsonConvert.SerializeObject(new SensorPayload
            {
                Value = sensorValue.Value,
                LastChangeDateTime = sensorValue.LastChangeDateTime
            });

            try
            {
                var message = new MqttApplicationMessageBuilder()
                  .WithTopic(sensorValue.SensorName)
                  .WithPayload(payload)
                  .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce)
                  .WithRetainFlag()
                  .Build();

                if (_mqttClient.IsConnected)
                {
                    await _mqttClient.PublishAsync(message);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e.Message);
            }
        }

        public async Task StartSimulation(int simulationCount, CancellationToken cancellationToken)
        {
            _sensorValueSimulator.StartSimulation(simulationCount, cancellationToken);

            foreach (var sensorValue in _sensorValueSimulator.Sensors.Values)
            {
                await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(sensorValue.SensorName).Build());
            }
        }

    }
}
