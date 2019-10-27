using System;

namespace CopaData.Drivers.Samples.Rest
{
    public class SensorPayload
    {
        public double Value { get; set; }

        public DateTime LastChangeDateTime { get; set; }
    }
}
