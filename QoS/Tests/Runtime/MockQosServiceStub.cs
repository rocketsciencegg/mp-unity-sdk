using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RocketScience.Services.Qos.Models;

namespace RocketScience.Services.Qos.Tests
{
    public class MockQosServiceStub : TestStub, IQosService
    {
        public Func<string, IList<string>, IList<IQosResult>> MockLOrderRegionsByQoS { get; set; }
        public Func<IList<string>, IList<IQosAnnotatedResult>> MockLOrderRelayQoS { get; set; }
        public Func<IList<string>, IList<IQosAnnotatedResult>> MockLOrderMultiplayQoS { get; set; }

        public Task<IList<IQosResult>> GetSortedQosResultsAsync(string service, IList<string> regions)
        {
            AddCall(nameof(GetSortedQosResultsAsync), service, regions);
            return Task.FromResult(MockLOrderRegionsByQoS(service, regions));
        }

        public Task<IList<IQosAnnotatedResult>> GetSortedRelayQosResultsAsync(IList<string> regions)
        {
            AddCall(nameof(GetSortedRelayQosResultsAsync), regions);
            return Task.FromResult(MockLOrderRelayQoS(regions));
        }

        public Task<IList<IQosAnnotatedResult>> GetSortedMultiplayQosResultsAsync(IList<string> fleet)
        {
            AddCall(nameof(GetSortedMultiplayQosResultsAsync), fleet);
            return Task.FromResult(MockLOrderMultiplayQoS(fleet));
        }

        public Task<IList<Models.QosServer>> GetAllServersAsync()
        {
            throw new NotImplementedException();
        }

        public Task<IList<(QosServer, IQosMeasurements)>> GetQosResultsAsync(IList<QosServer> servers)
        {
            throw new NotImplementedException();
        }
    }
}
