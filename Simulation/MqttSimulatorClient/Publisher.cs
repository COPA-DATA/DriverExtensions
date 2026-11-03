using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Formatter;
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
        private volatile bool _stopping;

        // The broker also sends the messages published by this client back to it (MQTT 3.1.1 has no NoLocal
        // subscription option). Published payloads are remembered here until they come back and are then ignored,
        // otherwise a delayed echo of an older value could overwrite a newer simulated value. Only values written
        // by others are applied. The limit keeps echoes that never arrive (e.g. after a disconnect) from piling up.
        private const int MaxPendingEchoesPerTopic = 10;
        private readonly ConcurrentDictionary<string, List<string>> _pendingEchoes = new ConcurrentDictionary<string, List<string>>();

        public Publisher()
        {
            _sensorValueSimulator = new SensorValueSimulator();
            _sensorValueSimulator.SensorValueChanged += async (sender, value) => await SensorValueChanged(sender, value);
            var factory = new MqttClientFactory();
            _mqttClient = factory.CreateMqttClient();
        }

        public async Task ConnectAsync(string serverAddress, int serverPort)
        {
            // MQTTnet defaults to MQTT 5.0; the simulator uses MQTT 3.1.1 to work with older brokers.
            var options = new MqttClientOptionsBuilder()
                .WithClientId("producer_{268EEB35-2130-49AA-A757-E4D2B05B5D71}")
                .WithTcpServer(serverAddress, serverPort)
                .WithProtocolVersion(MqttProtocolVersion.V311)
                .WithCleanSession()
                .Build();

            await _mqttClient.ConnectAsync(options, CancellationToken.None);

            _mqttClient.DisconnectedAsync += async e =>
            {
                if (_stopping)
                {
                    return;
                }

                Logger.Warn("### DISCONNECTED FROM SERVER ###");
                await Task.Delay(TimeSpan.FromSeconds(5));

                try
                {
                    await _mqttClient.ReconnectAsync();
                    await SubscribeSensorsAsync();
                }
                catch
                {
                    Logger.Error("### RECONNECTING FAILED ###");
                }
            };

            _mqttClient.ApplicationMessageReceivedAsync += args =>
            {
                var topic = args.ApplicationMessage.Topic;

                try
                {
                    var payloadString = Encoding.UTF8.GetString(args.ApplicationMessage.Payload);
                    if (TryRemovePendingEcho(topic, payloadString))
                    {
                        return Task.CompletedTask;
                    }

                    var sensorPayload = JsonConvert.DeserializeObject<SensorPayload>(payloadString);

                    if (sensorPayload != null && _sensorValueSimulator.Sensors.TryGetValue(topic, out var sensorValue))
                    {
                        sensorValue.Value = sensorPayload.Value;
                        sensorValue.LastChangeDateTime = sensorPayload.LastChangeDateTime;
                    }
                }
                catch (JsonException e)
                {
                    Logger.Error($"Received invalid payload on topic '{topic}': {e.Message}");
                }

                return Task.CompletedTask;
            };
        }

        public async Task DisconnectAsync()
        {
            _stopping = true;

            if (_mqttClient.IsConnected)
            {
                await _mqttClient.DisconnectAsync();
            }
        }

        private async Task SensorValueChanged(object? sender, SensorValue sensorValue)
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
                    // Registered before publishing, as the echo can arrive before PublishAsync returns.
                    AddPendingEcho(sensorValue.SensorName, payload);
                    await _mqttClient.PublishAsync(message);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e.Message);
            }
        }

        private void AddPendingEcho(string topic, string payload)
        {
            var pending = _pendingEchoes.GetOrAdd(topic, _ => new List<string>());
            lock (pending)
            {
                pending.Add(payload);
                if (pending.Count > MaxPendingEchoesPerTopic)
                {
                    pending.RemoveAt(0);
                }
            }
        }

        private bool TryRemovePendingEcho(string topic, string payload)
        {
            if (!_pendingEchoes.TryGetValue(topic, out var pending))
            {
                return false;
            }

            lock (pending)
            {
                return pending.Remove(payload);
            }
        }

        private async Task SubscribeSensorsAsync()
        {
            foreach (var sensorValue in _sensorValueSimulator.Sensors.Values)
            {
                await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(sensorValue.SensorName).Build());
            }
        }

        public async Task RunSimulationAsync(int simulationCount, CancellationToken cancellationToken)
        {
            // Creates the sensors synchronously and returns when the first values have been published.
            var simulation = _sensorValueSimulator.RunSimulationAsync(simulationCount, cancellationToken);

            try
            {
                await SubscribeSensorsAsync();
            }
            catch (Exception e)
            {
                Logger.Error($"Subscribing sensors failed: {e.Message}");
            }

            await simulation;
        }
    }
}
