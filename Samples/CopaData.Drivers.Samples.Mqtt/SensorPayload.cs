using System;

namespace CopaData.Drivers.Samples.Mqtt
{
    public class SensorPayload
    {
        public double Value { get; set; }

        public DateTime LastChangeDateTime { get; set; }
    }
}
