using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CopaData.Drivers.Contracts;
using Microsoft.Extensions.Configuration;
using RestSharp;

namespace CopaData.Drivers.Samples.Rest
{
    public class DriverExtension : IDriverExtension
    {
        private const string DefaultServerAddress = "http://localhost:5000/";
        private const int MaxParallelRequests = 10;
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromMilliseconds(2000);

        private ILogger _logger = null!;
        private IValueCallback _valueCallback = null!;
        private RestClient _restClient = null!;
        private readonly HashSet<string> _subscriptions;
        private readonly object _subscriptionsLock = new object();

        public DriverExtension()
        {
            _subscriptions = new HashSet<string>();
        }

        public Task InitializeAsync(ILogger logger, IValueCallback valueCallback, string configFilePath)
        {
            _logger = logger;
            _valueCallback = valueCallback;

            var configuration = GetConfiguration(configFilePath);
            var serverAddress = configuration["ServerAddress"] ?? DefaultServerAddress;
            _restClient = new RestClient(serverAddress);

            return Task.CompletedTask;
        }

        private IConfigurationRoot GetConfiguration(string configFilePath)
        {
            _logger.DeepDebug($"Using configuration file at '{configFilePath}'");
            var configurationBuilder = new ConfigurationBuilder()
                .AddJsonFile(configFilePath, optional: true);

            return configurationBuilder.Build();
        }

        public Task ShutdownAsync()
        {
            _restClient.Dispose();

            return Task.CompletedTask;
        }

        public Task<bool> SubscribeAsync(string symbolicAddress)
        {
            lock (_subscriptionsLock)
            {
                _subscriptions.Add(symbolicAddress);
            }

            return Task.FromResult(true);
        }

        public Task UnsubscribeAsync(string symbolicAddress)
        {
            lock (_subscriptionsLock)
            {
                _subscriptions.Remove(symbolicAddress);
            }

            return Task.CompletedTask;
        }

        public async Task ReadAllAsync()
        {
            string[] subscriptions;
            lock (_subscriptionsLock)
            {
                subscriptions = _subscriptions.ToArray();
            }

            var results = new ConcurrentDictionary<string, SensorPayload?>();
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = MaxParallelRequests };

            await Parallel.ForEachAsync(subscriptions, parallelOptions, async (subscription, cancellationToken) =>
            {
                results[subscription] = await ReadSensorAsync(subscription, cancellationToken);
            });

            // Values are reported from a single thread, as the callback is not documented to be thread-safe.
            foreach (var subscription in subscriptions)
            {
                var sensorPayload = results[subscription];
                if (sensorPayload != null)
                {
                    _valueCallback.SetValue(subscription, sensorPayload.Value, sensorPayload.LastChangeDateTime, StatusBits.Spontaneous);
                }
                else
                {
                    _valueCallback.SetValue(subscription, StatusBits.Invalid);
                }
            }
        }

        private async Task<SensorPayload?> ReadSensorAsync(string symbolicAddress, CancellationToken cancellationToken)
        {
            var request = new RestRequest("sensors", Method.Get) { Timeout = RequestTimeout };
            request.AddQueryParameter("sensorName", symbolicAddress);

            try
            {
                var response = await _restClient.ExecuteAsync<SensorPayload>(request, cancellationToken);
                if (response.IsSuccessful && response.Data != null)
                {
                    return response.Data;
                }

                var error = response.ErrorMessage ?? $"HTTP {(int)response.StatusCode} {response.StatusCode}";
                _logger.Error($"Error while requesting symbolic address '{symbolicAddress}' from Web Service: " + error);
            }
            catch (Exception e)
            {
                _logger.Error($"Exception while requesting symbolic address '{symbolicAddress}' from Web Service: " + e.Message);
            }

            return null;
        }

        public Task<bool> WriteStringAsync(string symbolicAddress, string value, DateTime dateTime, StatusBits statusBits)
        {
            return Task.FromResult(false);
        }

        public async Task<bool> WriteNumericAsync(string symbolicAddress, double value, DateTime dateTime, StatusBits statusBits)
        {
            var request = new RestRequest("sensors", Method.Put) { Timeout = RequestTimeout };
            request.AddQueryParameter("sensorName", symbolicAddress);
            request.AddQueryParameter("value", value.ToString(CultureInfo.InvariantCulture));

            try
            {
                var response = await _restClient.ExecuteAsync(request);
                return response.IsSuccessful;
            }
            catch (Exception e)
            {
                _logger.Error($"Error while setting value for symbolic address '{symbolicAddress}': " + e.Message);
            }

            return false;
        }
    }
}
