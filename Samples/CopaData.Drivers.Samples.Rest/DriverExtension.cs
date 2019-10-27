using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using CopaData.Drivers.Contracts;
using RestSharp;

namespace CopaData.Drivers.Samples.Rest
{
    public class DriverExtension : IDriverExtension
    {
        private ILogger _logger;
        private IValueCallback _valueCallback;
        private readonly IRestClient _restClient;
        private readonly List<string> _subscriptions;

        public DriverExtension()
        {
            _restClient = new RestClient("https://localhost:44301/");
            _subscriptions = new List<string>();
        }

        public Task InitializeAsync(ILogger logger, IValueCallback valueCallback, string configFilePath)
        {
            _logger = logger;
            _valueCallback = valueCallback;

            return Task.CompletedTask;
        }

        public Task ShutdownAsync()
        {
            return Task.CompletedTask;
        }

        public Task<bool> SubscribeAsync(string symbolicAddress)
        {
            _subscriptions.Add(symbolicAddress);
            return Task.FromResult(true);
        }

        public Task UnsubscribeAsync(string symbolicAddress)
        {
            _subscriptions.Remove(symbolicAddress);

            return Task.CompletedTask;
        }

        public async Task ReadAllAsync()
        {
            foreach (var subscription in _subscriptions)
            {
                var request = new RestRequest("sensors", Method.GET) {Timeout = 2000};
                request.AddQueryParameter("sensorName", subscription);

                try
                {
                    var response = await _restClient.ExecuteTaskAsync<SensorPayload>(request);
                    if (response.IsSuccessful)
                    {
                        _valueCallback.SetValue(subscription, response.Data.Value, response.Data.LastChangeDateTime, StatusBits.Spontaneous);
                    }
                    else
                    {
                        _logger.Error($"Error while requesting symbolic address '{subscription}' from Web Service: " + response.ErrorMessage);

                        _valueCallback.SetValue(subscription, StatusBits.Invalid);
                    }
                }
                catch (Exception e)
                {
                    _logger.Error($"Exception while requesting symbolic address '{subscription}' from Web Service: " + e.Message);
                    _valueCallback.SetValue(subscription, StatusBits.Invalid);
                }
            }
        }

        public Task<bool> WriteStringAsync(string symbolicAddress, string value, DateTime dateTime, StatusBits status)
        {
            return Task.FromResult(false);
        }

        public async Task<bool> WriteNumericAsync(string symbolicAddress, double value, DateTime dateTime, StatusBits status)
        {
            var request = new RestRequest("sensors", Method.PUT);
            request.AddQueryParameter("sensorName", symbolicAddress);
            request.AddQueryParameter("value", value.ToString(CultureInfo.InvariantCulture));

            try
            {
                var response = await _restClient.ExecuteTaskAsync<SensorPayload>(request);
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
