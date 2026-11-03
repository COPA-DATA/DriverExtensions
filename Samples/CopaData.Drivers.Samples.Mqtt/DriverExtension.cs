using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CopaData.Drivers.Contracts;
using Microsoft.Extensions.Configuration;
using MQTTnet;
using Newtonsoft.Json;

namespace CopaData.Drivers.Samples.Mqtt
{
    public class DriverExtension : IDriverExtension
    {
        private static readonly TimeSpan ReconnectInterval = TimeSpan.FromSeconds(5);

        private ILogger _logger = null!;
        private IValueCallback _valueCallback = null!;
        private readonly IMqttClient _mqttClient;
        private readonly HashSet<string> _subscriptions;
        private readonly object _subscriptionsLock = new object();
        private readonly CancellationTokenSource _shutdown = new CancellationTokenSource();
        private MqttClientOptions _options = null!;
        private Task _connectionMonitor = Task.CompletedTask;

        public DriverExtension()
        {
            _subscriptions = new HashSet<string>();
            var factory = new MqttClientFactory();
            _mqttClient = factory.CreateMqttClient();
        }

        public async Task InitializeAsync(ILogger logger, IValueCallback valueCallback, string configFilePath)
        {
            _logger = logger;
            _valueCallback = valueCallback;

            var configuration = GetConfiguration(configFilePath);
            var serverAddress = configuration["MqttServerAddress"] ?? "localhost";
            var clientId = configuration["ClientId"] ?? "myLocalClientId";

            // Create TCP based options using the builder.
            _options = new MqttClientOptionsBuilder()
                .WithClientId(clientId)
                .WithTcpServer(serverAddress)
                //   .WithCredentials("bud", "%spencer%")
                //     .WithTls()
                .WithCleanSession()
                .Build();

            // Register the handlers before connecting, so no event of the first connection is missed.
            _mqttClient.DisconnectedAsync += OnDisconnectedAsync;
            _mqttClient.ApplicationMessageReceivedAsync += OnApplicationMessageReceivedAsync;

            await TryConnectAsync(_shutdown.Token);

            // Reconnects whenever the connection is lost, including when the server was not reachable at startup.
            _connectionMonitor = Task.Run(() => MonitorConnectionAsync(_shutdown.Token));
        }

        private IConfigurationRoot GetConfiguration(string configFilePath)
        {
            _logger.DeepDebug($"Using configuration file at '{configFilePath}'");
            var configurationBuilder = new ConfigurationBuilder()
                .AddJsonFile(configFilePath, optional: true);

            var configuration = configurationBuilder.Build();
            return configuration;
        }

        private async Task MonitorConnectionAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(ReconnectInterval, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (!_mqttClient.IsConnected)
                {
                    await TryConnectAsync(cancellationToken);
                }
            }
        }

        private async Task TryConnectAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _mqttClient.ConnectAsync(_options, cancellationToken);

                // The session is clean, so the server does not remember subscriptions of an earlier connection.
                foreach (var symbolicAddress in GetSubscriptions())
                {
                    await _mqttClient.SubscribeAsync(
                        new MqttTopicFilterBuilder().WithTopic(symbolicAddress).Build(), cancellationToken);
                }

                _logger.DeepDebug("Connected to MQTT server");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception e)
            {
                _logger.Error($"Connecting to MQTT server failed: {e.Message}");
            }
        }

        private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs e)
        {
            // Also raised for failed connection attempts and for the disconnect during shutdown.
            if (!e.ClientWasConnected || _shutdown.IsCancellationRequested)
            {
                return Task.CompletedTask;
            }

            _logger.Warn("### DISCONNECTED FROM SERVER ###");

            foreach (var symbolicAddress in GetSubscriptions())
            {
                _valueCallback.SetValue(symbolicAddress, StatusBits.Invalid);
            }

            return Task.CompletedTask;
        }

        private Task OnApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            var topic = args.ApplicationMessage.Topic;

            try
            {
                var payload = Encoding.UTF8.GetString(args.ApplicationMessage.Payload);
                var sensorPayload = JsonConvert.DeserializeObject<SensorPayload>(payload);

                if (sensorPayload != null)
                {
                    _valueCallback.SetValue(topic, sensorPayload.Value, sensorPayload.LastChangeDateTime);
                    return Task.CompletedTask;
                }

                _logger.Error($"Received empty payload on topic '{topic}'");
            }
            catch (JsonException e)
            {
                _logger.Error($"Received invalid payload on topic '{topic}': {e.Message}");
            }

            _valueCallback.SetValue(topic, StatusBits.Invalid);
            return Task.CompletedTask;
        }

        private string[] GetSubscriptions()
        {
            lock (_subscriptionsLock)
            {
                return _subscriptions.ToArray();
            }
        }

        public Task ReadAllAsync()
        {
            return Task.CompletedTask;
        }

        public async Task ShutdownAsync()
        {
            _shutdown.Cancel();
            await _connectionMonitor;

            if (_mqttClient.IsConnected)
            {
                await _mqttClient.DisconnectAsync();
            }

            _mqttClient.Dispose();
        }

        public async Task<bool> SubscribeAsync(string symbolicAddress)
        {
            lock (_subscriptionsLock)
            {
                if (!_subscriptions.Add(symbolicAddress))
                {
                    return true;
                }
            }

            // While disconnected, the topic is subscribed as soon as the connection is established.
            if (!_mqttClient.IsConnected)
            {
                return true;
            }

            try
            {
                await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(symbolicAddress).Build());
                return true;
            }
            catch (Exception e)
            {
                _logger.Error($"Error while subscribing '{symbolicAddress}': {e.Message}");
            }

            lock (_subscriptionsLock)
            {
                _subscriptions.Remove(symbolicAddress);
            }

            return false;
        }

        public async Task UnsubscribeAsync(string symbolicAddress)
        {
            lock (_subscriptionsLock)
            {
                _subscriptions.Remove(symbolicAddress);
            }

            if (!_mqttClient.IsConnected)
            {
                return;
            }

            try
            {
                await _mqttClient.UnsubscribeAsync(symbolicAddress);
            }
            catch (Exception e)
            {
                _logger.Error($"Error while unsubscribing '{symbolicAddress}': {e.Message}");
            }
        }

        public async Task<bool> WriteNumericAsync(string symbolicAddress, double value, DateTime dateTime, StatusBits statusBits)
        {
            var sensorPayload = new SensorPayload() { Value = value, LastChangeDateTime = dateTime };
            var payloadString = JsonConvert.SerializeObject(sensorPayload);

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(symbolicAddress)
                .WithPayload(payloadString)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce)
                .WithRetainFlag()
                .Build();

            try
            {
                await _mqttClient.PublishAsync(message);
                return true;
            }
            catch (Exception e)
            {
                _logger.Error("Error while submitting value: " + e.Message);
            }

            return false;
        }

        public Task<bool> WriteStringAsync(string symbolicAddress, string value, DateTime dateTime, StatusBits statusBits)
        {
            return Task.FromResult(false);
        }
    }
}
