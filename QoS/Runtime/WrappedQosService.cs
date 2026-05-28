using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Unity.Services.Authentication.Internal;
using RocketScience.Services.Qos.Apis.QosDiscovery;
using RocketScience.Services.Qos.QosDiscovery;
using RocketScience.Services.Qos.Runner;
using RocketScience.Services.Qos.Models;

namespace RocketScience.Services.Qos
{
    class WrappedQosService : IQosService
    {
        const string ResultLatencyMetricName = "qos_result_latency_ms";
        const string ResultPacketLossMetricName = "qos_result_packet_loss";

        const string MetricServiceNameLabelName = "qos_service_name";
        const string MetricServiceRegionLabelName = "qos_service_region";
        const string MetricClientCountryLabelName = "qos_client_country";
        const string MetricClientRegionLabelName = "qos_client_region";
        const string MetricClientBestResultLabelName = "qos_best_result";
        const string MetricClientBestResultLabelTrueValue = "true";

        IQosDiscoveryApiClient _qosDiscoveryApiClient;

        IQosRunner _qosRunner;

        IAccessToken _accessToken;

        string _latestCountryForTelemetry;
        string _latestRegionForTelemetry;
        string _getAllServersEtag = "";
        IList<QosServer> _getAllServersCached;

        internal WrappedQosService(IQosDiscoveryApiClient qosDiscoveryApiClient, IQosRunner qosRunner,
                                   IAccessToken accessToken)
        {
            _qosDiscoveryApiClient = qosDiscoveryApiClient;
            _qosRunner = qosRunner;
            _accessToken = accessToken;
        }

        List<QosAnnotatedResult> SortResults(IList<QosAnnotatedResult> results)
        {
            return results
                .OrderBy(q => q.AverageLatencyMs)
                .ThenBy(q => q.PacketLossPercent)
                .ToList();
        }

        public async Task<IList<IQosAnnotatedResult>> GetSortedMultiplayQosResultsAsync(IList<string> fleetId)
        {
            return await GetSortedInternalServiceQosResultsAsync("multiplay", fleetId);
        }

        // GetAllServersAsync is not safe to run concurrently with GetQosResultsAsync or with itself.
        public async Task<IList<QosServer>> GetAllServersForFleetAsync(string fleetId)
        {
            //var rq = new GetAllServersRequest();
            var rq = new ListServersRequest(fleetId);

            var headers = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(_getAllServersEtag))
                headers.Add("If-None-Match", _getAllServersEtag);
            var cfg = new Configuration(null, null, null, headers);

            try
            {
                var httpResp = await _qosDiscoveryApiClient.ListServersAsync(rq, cfg);
                httpResp.Headers.TryGetValue("ETag", out _getAllServersEtag);
                _getAllServersCached = httpResp.Result.Servers;
                httpResp.Headers.TryGetValue("X-Client-Country", out _latestCountryForTelemetry);
                httpResp.Headers.TryGetValue("X-Client-Region", out _latestRegionForTelemetry);
            }
            catch (Http.HttpException ex)
            {
                if ((long)HttpStatusCode.NotModified == ex.Response.StatusCode)
                    return _getAllServersCached;
                throw;
            }

            return _getAllServersCached;
        }

        // GetQosResultsAsync is not safe to run concurrently with GetAllServersAsync.
        public async Task<IList<(QosServer, IQosMeasurements)>> GetQosResultsAsync(IList<QosServer> servers)
        {
            var qosResults = await _qosRunner.MeasureQosV2Async(servers);
            //SendResultsMetricsV2(qosResults);
            return qosResults;
        }

        //void SendResultsMetricsV2(IReadOnlyCollection<(QosServer, IQosMeasurements)> qosResults)
        //{
        //    SendResultsMetricsV2ForService(qosResults, qs => qs.Annotations.RelayRegionId, "relay");
        //    SendResultsMetricsV2ForService(qosResults, qs => qs.Annotations.MultiplayRegionId, "multiplay");
        //}

        void SendResultsMetricsV2ForService(IReadOnlyCollection<(QosServer, IQosMeasurements)> allResults,
            Func<QosServer, List<string>> regionGetter, string service)
        {
            // the list is ordered only to get a rough idea of which server is the best.
            var results = allResults.Where(t => regionGetter(t.Item1) != null && regionGetter(t.Item1).Count > 0)
                .OrderBy(t => t.Item2.AverageLatencyMs)
                .ThenBy(t => t.Item2.PacketLossPercent).ToList();
            for (var i = 0; i < results.Count; i++)
            {
                var result = results[i];
                SendResultMetrics(service,
                    _latestCountryForTelemetry,
                    _latestRegionForTelemetry,
                    regionGetter(result.Item1)[0],  // this code assumes that a server has no more than one region.
                    result.Item2.AverageLatencyMs,
                    result.Item2.PacketLossPercent,
                    i == 0);
            }
        }

        // RS-JH: Commented out as may re-implement in future to take in a number of fleet Ids to be iterated over
        //  the new Spec only takes 1 ID though, not a list.
        internal async Task<IList<IQosAnnotatedResult>> GetSortedInternalServiceQosResultsAsync(string service, IList<string> fleet)
        {
#if UGS_QOS_SUPPORTED && !UNITY_WEBGL
            if (string.IsNullOrEmpty(_accessToken.AccessToken))
            {
                throw new Exception("Access token not available, please sign in with the Authentication Service.");
            }

            var fleetList = fleet as List<string>;
            // Else create a List from the IList
            if (fleetList == null && fleet != null)
            {
                fleetList = new List<string>(fleet);
            }

            List<QosServer> servers = new List<QosServer>();
            Response<QosServers> lastResp = null;
            foreach (string fleetId in fleet)
            {

                Response<QosServers> httpResp = await _qosDiscoveryApiClient.ListServersAsync(
                    new ListServersRequest(fleetId));
                servers.AddRange(httpResp.Result.Servers);
                lastResp = httpResp;
            }

            // empty response, return no results
            if (!servers.Any())
            {
                return new List<IQosAnnotatedResult>();
            }

            var qosResults = await _qosRunner.MeasureQosAsync(servers);
            IList<IQosAnnotatedResult> sortedResults = SortServiceResults(qosResults);

            SendResultsMetrics(sortedResults.Cast<IQosResult>().ToList(), service, lastResp);
            return sortedResults;
#else
#if UNITY_WEBGL
            throw new PlatformNotSupportedException(
                "QoS SDK does not support WebGL at this time.");
#else
            throw new UnsupportedEditorVersionException(
                "QoS SDK does not support this version of Unity, please upgrade to 2020.3.34f1+, 2021.3.2f1+, 2022.2.0a10+, or newer.");
#endif
#endif
        }


        internal async Task<IList<IQosAnnotatedResult>> GetSortedInternalServiceQosResultsAsync(string service, string fleet)
        {
#if UGS_QOS_SUPPORTED && !UNITY_WEBGL
            if (string.IsNullOrEmpty(_accessToken.AccessToken))
            {
                throw new Exception("Access token not available, please sign in with the Authentication Service.");
            }


            Response<Models.QosServers> httpResp = await _qosDiscoveryApiClient.ListServersAsync(
                new ListServersRequest(fleet));
            List<Models.QosServer> servers = httpResp.Result.Servers;

            // empty response, return no results
            if (!servers.Any())
            {
                return new List<IQosAnnotatedResult>();
            }

            var qosResults = await _qosRunner.MeasureQosAsync(servers);
            var sortedResults = SortServiceResults(qosResults);

            SendResultsMetrics(sortedResults.Cast<IQosResult>().ToList(), service, httpResp);
            return sortedResults;
#else
#if UNITY_WEBGL
            throw new PlatformNotSupportedException(
                "QoS SDK does not support WebGL at this time.");
#else
            throw new UnsupportedEditorVersionException(
                "QoS SDK does not support this version of Unity, please upgrade to 2020.3.34f1+, 2021.3.2f1+, 2022.2.0a10+, or newer.");
#endif
#endif
        }

        List<IQosAnnotatedResult> SortServiceResults(IList<QosAnnotatedResult> results)
        {
            // When converting from internal qos results to qos results we use int.MaxValue for invalid latency, so we filter those out.
            // A valid result will have packet loss equals to zero and lower than one. Anything else is considered invalid as either
            // float.MaxValue or 100% of packet loss means we could not get any measures.
            return results
                .Where(q => q.AverageLatencyMs != int.MaxValue && q.PacketLossPercent >= 0.0 &&
                q.PacketLossPercent < 1.0)
                .GroupBy(q => q.Region)
                .Select(q => (IQosAnnotatedResult)new QosResult(q.Key,
                    (int)Math.Round(q.Select(x => x.AverageLatencyMs).Average()),
                    q.Select(x => x.PacketLossPercent).Average(),
                    q.Select(x => x.Annotations).First()))
                .ToList()
                .OrderBy(q => q.AverageLatencyMs)
                .ThenBy(q => q.PacketLossPercent)
                .ToList();
        }

        void SendResultsMetrics(IList<QosAnnotatedResult> sortedResults, string service, Response discoveryResponse)
        {
            discoveryResponse.Headers.TryGetValue("X-Client-Country", out var clientCountry);
            discoveryResponse.Headers.TryGetValue("X-Client-Region", out var clientRegion);

            for (var index = 0; index < sortedResults.Count; index++)
            {
                var result = sortedResults[index];
                SendResultMetrics(service, clientCountry, clientRegion, result.Region, result.AverageLatencyMs,
                    result.PacketLossPercent, index == 0);
            }
        }

        void SendResultsMetrics(IList<IQosResult> sortedResults, string service, Response discoveryResponse)
        {
            discoveryResponse.Headers.TryGetValue("X-Client-Country", out var clientCountry);
            discoveryResponse.Headers.TryGetValue("X-Client-Region", out var clientRegion);

            for (var index = 0; index < sortedResults.Count; index++)
            {
                var result = sortedResults[index];
                SendResultMetrics(service, clientCountry, clientRegion, result.Region, result.AverageLatencyMs,
                    result.PacketLossPercent, index == 0);
            }
        }

        void SendResultMetrics(string service, string clientCountry, string clientRegion, string region, int averageLatencyMs,
            float packetLossPercent, bool isBest)
        {
            IDictionary<string, string> metricTags = new Dictionary<string, string>();

            metricTags.Add(MetricServiceNameLabelName, service);
            metricTags.Add(MetricServiceRegionLabelName, region);

            if (!string.IsNullOrEmpty(clientCountry))
                metricTags.Add(MetricClientCountryLabelName, clientCountry);

            if (!string.IsNullOrEmpty(clientRegion))
                metricTags.Add(MetricClientRegionLabelName, clientRegion);

            // only add the "best_result" tag when processing the first (best) result
            if (isBest)
                metricTags.Add(MetricClientBestResultLabelName, MetricClientBestResultLabelTrueValue);

            // TODO(Jac Griffiths): Replace with an RS telemetry backend, or remove in not applicable
            //_metrics.SendHistogramMetric(ResultLatencyMetricName, averageLatencyMs, metricTags);
            //_metrics.SendHistogramMetric(ResultPacketLossMetricName, packetLossPercent, metricTags);
        }

        IQosResult MapToPublicQosResult(IQosAnnotatedResult internalQosResult)
        {
            return new QosResult(internalQosResult.Region, internalQosResult.AverageLatencyMs,
                internalQosResult.PacketLossPercent);
        }
    }

    class QosResult : IQosAnnotatedResult
    {
        public QosResult(string region, int averageLatencyMs, float packetLossPercent,
                         Dictionary<string, List<string>> annotations = default)
        {
            Region = region;
            AverageLatencyMs = averageLatencyMs;
            PacketLossPercent = packetLossPercent;
            Annotations = annotations ?? new Dictionary<string, List<string>>();
        }

        public string Region { get; }
        public int AverageLatencyMs { get; }
        public float PacketLossPercent { get; }
        public Dictionary<string, List<string>> Annotations { get; }
    }

    class UnsupportedEditorVersionException : Exception
    {
        public UnsupportedEditorVersionException()
        {
        }

        public UnsupportedEditorVersionException(string message) : base(message)
        {
        }
    }
}
