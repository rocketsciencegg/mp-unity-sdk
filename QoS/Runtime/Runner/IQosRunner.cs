using System.Collections.Generic;
using System.Threading.Tasks;
using RocketScience.Services.Qos.Models;

namespace RocketScience.Services.Qos.Runner
{
    interface IQosRunner
    {
        // Returning a List for simpler sorting (IList doesn't have a Sort method)
        Task<List<QosAnnotatedResult>> MeasureQosAsync(IList<QosServer> servers);
        Task<List<(QosServer, IQosMeasurements)>> MeasureQosV2Async(IList<QosServer> servers);
    }
}
