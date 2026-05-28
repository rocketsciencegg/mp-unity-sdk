using System.Collections.Generic;

using Unity.Services.Core.Telemetry.Internal;

namespace RocketScience.Services.WireDirect.Tests.Stubs
{
    public class MetricsMock : IMetrics
    {
        public void SendGaugeMetric(string name, double value = 0, IDictionary<string, string> tags = null)
        {
        }

        public void SendHistogramMetric(string name, double time, IDictionary<string, string> tags = null)
        {
        }

        public void SendSumMetric(string name, double value = 1, IDictionary<string, string> tags = null)
        {
        }
    }
}
