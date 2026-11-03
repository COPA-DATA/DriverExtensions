using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using CopaData.Drivers.Contracts;

namespace CopaData.Drivers.Samples.Basic
{
    public class DriverExtension : IDriverExtension
    {
        private ILogger _logger = null!;
        private IValueCallback _valueCallback = null!;

        // Values are kept separately from the subscriptions, so writing to a variable that is not
        // subscribed stores the value without reporting the variable in ReadAllAsync.
        private readonly ConcurrentDictionary<string, object> _values;
        private readonly ConcurrentDictionary<string, byte> _subscriptions;

        public DriverExtension()
        {
            _values = new ConcurrentDictionary<string, object>();
            _subscriptions = new ConcurrentDictionary<string, byte>();
        }

        public Task InitializeAsync(ILogger logger, IValueCallback valueCallback, string configFilePath)
        {
            _logger = logger;
            _valueCallback = valueCallback;

            return Task.CompletedTask;
        }

        public Task ShutdownAsync()
        {
            _subscriptions.Clear();
            _values.Clear();

            return Task.CompletedTask;
        }

        public Task<bool> SubscribeAsync(string symbolicAddress)
        {
            _logger.DeepDebug($"Subscribe '{symbolicAddress}'");

            _subscriptions.TryAdd(symbolicAddress, 0);
            _values.TryAdd(symbolicAddress, 0d);

            return Task.FromResult(true);
        }

        public Task UnsubscribeAsync(string symbolicAddress)
        {
            _logger.DeepDebug($"Unsubscribe '{symbolicAddress}'");

            _subscriptions.TryRemove(symbolicAddress, out _);
            return Task.CompletedTask;
        }

        public Task ReadAllAsync()
        {
            foreach (var symbolicAddress in _subscriptions.Keys)
            {
                if (!_values.TryGetValue(symbolicAddress, out var value))
                {
                    continue;
                }

                if (value is string stringValue)
                {
                    _valueCallback.SetValue(symbolicAddress, stringValue, StatusBits.Spontaneous);
                }
                else if (value is double doubleValue)
                {
                    _valueCallback.SetValue(symbolicAddress, doubleValue, StatusBits.Spontaneous);
                }
            }

            return Task.CompletedTask;
        }

        public Task<bool> WriteStringAsync(string symbolicAddress, string value, DateTime dateTime, StatusBits status)
        {
            _logger.DeepDebug($"WriteString '{symbolicAddress}'");

            _values[symbolicAddress] = value;
            return Task.FromResult(true);
        }

        public Task<bool> WriteNumericAsync(string symbolicAddress, double value, DateTime dateTime, StatusBits status)
        {
            _logger.DeepDebug($"WriteNumeric '{symbolicAddress}'");

            _values[symbolicAddress] = value;
            return Task.FromResult(true);
        }
    }
}
