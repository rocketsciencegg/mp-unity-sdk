using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using RocketScience.Networking.Qos;
using RocketScience.Services.Qos.Models;
using Debug = UnityEngine.Debug;

namespace RocketScience.Services.Qos.Runner
{
    delegate IQosJob QosJobProvider(IList<UcgQosServer> servers, string title);

    delegate Task<IPAddress[]> DnsResolver(string host);

    class BaselibQosRunner : IQosRunner
    {
        QosJobProvider _qosJobProvider = (servers, title) => new QosJob(servers, title);
        DnsResolver _dnsResolver = Dns.GetHostAddressesAsync;

        public BaselibQosRunner(QosJobProvider qosJobProvider = null, DnsResolver dnsResolver = null)
        {
            if (qosJobProvider != null)
            {
                _qosJobProvider = qosJobProvider;
            }

            if (dnsResolver != null)
            {
                _dnsResolver = dnsResolver;
            }
        }

        // MeasureQos will return an empty list if an error occurs
        public async Task<List<QosAnnotatedResult>> MeasureQosAsync(IList<QosServer> servers)
        {
            var convertedServers = (await Task.WhenAll(servers.Select(ToUcgFormat)))
                .Where(s => s.HasValue)
                .Select(s => s.Value)
                .ToList();

            var results = new List<QosAnnotatedResult>();
            var job = await RunQosJob(convertedServers);
            if (servers.Count() == job.QosResults.Count())
            {
                results = ParseResults(job.QosResults, servers);
            }

            job.Dispose();
            job.QosResults.Dispose();
            return results;
        }

        internal struct QosMeasurementImpl : IQosMeasurements
        {
            public QosMeasurementImpl(int averageLatencyMs, float packetLossPercent)
            {
                AverageLatencyMs = averageLatencyMs;
                PacketLossPercent = packetLossPercent;
            }

            public int AverageLatencyMs { get;  }
            public float PacketLossPercent { get; }
        }

        public async Task<List<(QosServer, IQosMeasurements)>> MeasureQosV2Async(IList<QosServer> servers)
        {
            var results = await MeasureQosAsync(servers);
            var output = new List<(QosServer, IQosMeasurements)>(capacity: results.Count);
            if (results.Count == 0 || results.Count != servers.Count) return output;  // something went wrong.
            var convertedResults = results.ConvertAll(qr => new QosMeasurementImpl(qr.AverageLatencyMs, qr.PacketLossPercent));
            for (var i = 0; i < servers.Count; i++)
                output.Add((servers[i], convertedResults[i]));

            return output;
        }

        async Task<IQosJob> RunQosJob(List<UcgQosServer> convertedServers)
        {
            var title = "QoS request";
            var job = _qosJobProvider(convertedServers, title);

            var handle = job.Schedule<QosJob>();
            while (!handle.IsCompleted)
            {
                await Task.Yield();
            }

            handle.Complete();

            return job;
        }

        // Helper method to covert a Discovery service QoS server model to the UCG QoS package server model.
        // the main difference is the 'address' and 'port' fields vs a 'endpoint' field.
        // If the 'endpoint' field cannot be parsed, this will return a null struct.
        Task<UcgQosServer?> ToUcgFormat(QosServer server)
        {
            var serverEndpoint = $"{server.Ipv4}:{server.Port}";
            var serverRegion = server.Regionid;
            return ToUcgFormat(serverEndpoint, serverRegion);
        }

        Task<UcgQosServer?> ToUcgFormat(string serverEndpoint, Guid serverRegion)
        {
            if (!Uri.TryCreate($"udp://{serverEndpoint}", UriKind.Absolute, out var uri))
            {
                Debug.LogError($"Could not create address from endpoint: '{serverEndpoint}'.");
                return Task.FromResult<UcgQosServer?>(null);
            }

            if (uri.Port == -1)
            {
                Debug.LogError($"Missing or invalid port in endpoint: '{serverEndpoint}'.");
                return Task.FromResult<UcgQosServer?>(null);
            }

            return MakeUcgQosServer();

            async Task<UcgQosServer?> MakeUcgQosServer()
            {
                // If uri.Host is an IP, Dns.GetHostAddressesAsync will just use
                // the address instead of attempting to resolve.
                var resolvedIps = await _dnsResolver(uri.Host);
                if (resolvedIps.Length == 0)
                {
                    Debug.LogError($"No addresses could be resolved for host {uri.Host}.");
                    return null;
                }
                var ip = GetIpAddress(resolvedIps);

                return new UcgQosServer
                {
                    regionid = serverRegion,
                    ipv4 = ip.AddressFamily == AddressFamily.InterNetwork ? ip.ToString() : null,
                    ipv6 = ip.AddressFamily == AddressFamily.InterNetworkV6 ? ip.ToString() : null,
                    port = Convert.ToUInt16(uri.Port),
                    BackoffUntilUtc = default
                };
            }

            static IPAddress GetIpAddress(in ReadOnlySpan<IPAddress> resolvedIps)
            {
#if UNITY_SWITCH || UNITY_PS4 || UNITY_PS5
                // On platforms that do not support IPv6, we take the first IPv4
                // address from the list of resolved IPs.
                foreach (var ipAddress in resolvedIps)
                {
                    if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return ipAddress;
                    }
                }

                return IPAddress.None;
#else
                // Choose first IP from list.
                // TODO: we could eventually provide ability to select IPv4/IPv6 specifically.
                return resolvedIps[0];
#endif
            }
        }

        static List<QosAnnotatedResult> ParseResults(IEnumerable<InternalQosResult> ucgResults,
            IEnumerable<QosServer> servers)
        {
            var results = new List<QosAnnotatedResult>();
            using var enr = servers.GetEnumerator();
            foreach (InternalQosResult r in ucgResults)
            {
                enr.MoveNext();
                if (enr.Current == null)
                {
                    break;
                }

                // avoid overflow when converting from uint to int
                int avgLatency = r.AverageLatencyMs > int.MaxValue ? int.MaxValue : (int)r.AverageLatencyMs;

                results.Add(new QosAnnotatedResult
                {
                    Region =
                        enr.Current
                            .Regionid.ToString(), // note: the results from ucg do not contain the region, so we assume they're in the same order as the servers list to populate the field.
                    AverageLatencyMs = avgLatency,
                    PacketLossPercent = r.PacketLoss
                });
            }

            return results;
        }
    }
}
