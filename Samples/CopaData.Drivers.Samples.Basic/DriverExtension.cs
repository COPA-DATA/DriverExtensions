using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CopaData.Drivers.Contracts;

namespace CopaData.Drivers.Samples.Basic
{
  public class DriverExtension : IDriverExtension
  {
    private ILogger _logger;
    private IValueCallback _valueCallback;
    private readonly Dictionary<string, object> _subscriptions;

    public DriverExtension()
    {
      _subscriptions = new Dictionary<string, object>();
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

      return Task.CompletedTask;
    }

    public Task<bool> SubscribeAsync(string symbolicAddress)
    {
      _logger.DeepDebug($"Subscribe '{symbolicAddress}'");

      _subscriptions.Add(symbolicAddress, null);
      return Task.FromResult(true);
    }

    public Task UnsubscribeAsync(string symbolicAddress)
    {
      _logger.DeepDebug($"Unsubscribe '{symbolicAddress}'");

      _subscriptions.Remove(symbolicAddress);
      return Task.CompletedTask;
    }

    public Task ReadAllAsync()
    {
      foreach (var variable in _subscriptions)
      {
        if (variable.Value != null)
        {
          if (variable.Value is string stringValue)
          {
            _valueCallback.SetValue(variable.Key, stringValue);
          }
          else if (variable.Value is double value)
          {
            _valueCallback.SetValue(variable.Key, value);
          }
        }
      }

      return Task.CompletedTask;
    }

    public Task<bool> WriteStringAsync(string symbolicAddress, string value, DateTime dateTime, StatusBits status)
    {
      _logger.DeepDebug($"WriteString '{symbolicAddress}'");

      _subscriptions[symbolicAddress] = value;
      return Task.FromResult(true);
    }

    public Task<bool> WriteNumericAsync(string symbolicAddress, double value, DateTime dateTime, StatusBits status)
    {
      _logger.DeepDebug($"WriteString '{symbolicAddress}'");

      _subscriptions[symbolicAddress] = value;
      return Task.FromResult(true);
    }
  }
}
