using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CopaData.Drivers.Contracts;
using Microsoft.Extensions.Configuration;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using Newtonsoft.Json;

namespace CopaData.Drivers.Samples.Mqtt
{
    public class DriverExtension : IDriverExtension
    {
        private ILogger _logger;
        private IValueCallback _valueCallback;
        private readonly IMqttClient _mqttClient;
        private readonly List<string> _subscriptions;

        public DriverExtension()
        {
            _subscriptions = new List<string>();
            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();
        }

        public async Task InitializeAsync(ILogger logger, IValueCallback valueCallback, string configFilePath)
        {
            _logger = logger;
            _valueCallback = valueCallback;

            var configuration = GetConfiguration(configFilePath);
            var serverAddress = configuration["MqttServerAddress"] ?? "127.0.0.1";
            var clientId = configuration["ClientId"] ?? "myLocalClientId";

            // Create TCP based options using the builder.
            var options = new MqttClientOptionsBuilder()
                .WithClientId(clientId)
                .WithTcpServer(serverAddress, 1883)
                //   .WithCredentials("bud", "%spencer%")
                //     .WithTls()
                .WithCleanSession()
                .Build();


            await _mqttClient.ConnectAsync(options, CancellationToken.None);

            _mqttClient.UseDisconnectedHandler(async e =>
            {
                if (!_mqttClient.IsConnected)
                {
                    foreach (var subscription in _subscriptions)
                    {
                        _valueCallback.SetValue(subscription, StatusBits.Invalid);
                    }
                }

                logger.Warn("### DISCONNECTED FROM SERVER ###");

                await Task.Delay(TimeSpan.FromSeconds(5));

                try
                {
                    await _mqttClient.ReconnectAsync();

                    foreach (var symbolicAddress in _subscriptions)
                    {
                        await _mqttClient.SubscribeAsync(
                            new TopicFilterBuilder().WithTopic(symbolicAddress).Build());
                    }
                }
                catch
                {
                    logger.Error("### RECONNECTING FAILED ###");
                }
            });

            _mqttClient.UseApplicationMessageReceivedHandler(args =>
            {
                var payload = Encoding.UTF8.GetString(args.ApplicationMessage.Payload);
                var t = JsonConvert.DeserializeObject<SensorPayload>(payload);

                _valueCallback.SetValue(args.ApplicationMessage.Topic, t.Value, t.LastChangeDateTime);
            });

        }

        private IConfigurationRoot GetConfiguration(string configFilePath)
        {
            _logger.Warn($"Using configuration file at '{configFilePath}'");
            var configurationBuilder = new ConfigurationBuilder()
                .AddJsonFile(configFilePath, optional: true);

            var configuration = configurationBuilder.Build();
            return configuration;
        }

        public Task ReadAllAsync()
        {
            return Task.CompletedTask;
        }

        public async Task ShutdownAsync()
        {
            await _mqttClient.DisconnectAsync();
        }

        public async Task<bool> SubscribeAsync(string symbolicAddress)
        {
            _subscriptions.Add(symbolicAddress);
            // Subscribe to a topic
            await _mqttClient.SubscribeAsync(new TopicFilterBuilder().WithTopic(symbolicAddress).Build());

            return true;
        }

        public async Task UnsubscribeAsync(string symbolicAddress)
        {
            _subscriptions.Remove(symbolicAddress);

            await _mqttClient.UnsubscribeAsync(symbolicAddress);
        }

        public async Task<bool> WriteNumericAsync(string symbolicAddress, double value, DateTime dateTime, StatusBits status)
        {
            var sensorPayload = new SensorPayload() { Value = value, LastChangeDateTime = dateTime };
            var payloadString = JsonConvert.SerializeObject(sensorPayload);

            var message = new MqttApplicationMessageBuilder()
              .WithTopic(symbolicAddress)
              .WithPayload(payloadString)
              .WithExactlyOnceQoS()
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

        public Task<bool> WriteStringAsync(string symbolicAddress, string value, DateTime dateTime, StatusBits status)
        {
            return Task.FromResult(false);
        }
    }
}
