using System;

namespace SensorSimulator
{
    public class SensorValue
    {
        public string SensorName { get; set; } = string.Empty;

        public double Value { get; set; }

        public DateTime LastChangeDateTime { get; set; }
    }
}
