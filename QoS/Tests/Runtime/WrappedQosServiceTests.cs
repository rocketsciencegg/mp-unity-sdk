using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading.Tasks;
using NUnit.Framework;
#if NUGET_MOQ_AVAILABLE && UNITY_EDITOR
using Moq;
#endif
using Unity.Services.Authentication.Internal;
using Unity.Services.Core.Telemetry.Internal;
using RocketScience.Services.Qos.Apis.QosDiscovery;
using RocketScience.Services.Qos.Http;
using RocketScience.Services.Qos.Models;
using RocketScience.Services.Qos.QosDiscovery;
using RocketScience.Services.Qos.Runner;
using UnityEngine.TestTools;
using QosServer = RocketScience.Services.Qos.Models.QosServer;

namespace RocketScience.Services.Qos.Tests
{
    class WrappedQosServiceTests
    {
#if NUGET_MOQ_AVAILABLE && UNITY_EDITOR
        Mock<IAccessToken> _accessTokenMock;
        Mock<IQosDiscoveryApiClient> _qosDiscoveryApiClientMock;
        Mock<V2.Apis.QosDiscovery.IQosDiscoveryApiClient> _qosDiscoveryApiClientMockV2;
        Mock<IQosRunner> _qosRunnerMock;
        Mock<IMetrics> _metricsMock;

        IQosService _qosService;

        [SetUp]
        public void Setup()
        {
            _accessTokenMock = new Mock<IAccessToken>();
            _accessTokenMock.Setup(x => x.AccessToken).Returns("mock-token");
            _qosDiscoveryApiClientMock = new Mock<IQosDiscoveryApiClient>();
            _qosDiscoveryApiClientMockV2 = new Mock<V2.Apis.QosDiscovery.IQosDiscoveryApiClient>();
            _qosRunnerMock = new Mock<IQosRunner>();
            _metricsMock = new Mock<IMetrics>();

            _qosService = new WrappedQosService(_qosDiscoveryApiClientMock.Object, _qosDiscoveryApiClientMockV2.Object,
                _qosRunnerMock.Object,
                _accessTokenMock.Object, _metricsMock.Object);
        }

#if UGS_QOS_SUPPORTED
        [UnityTest]
        public IEnumerator Test_GetSortedQoSResults_ThrowsIfNoAccessToken()
        {
            // given
            // override call in Setup()
            _accessTokenMock.Setup(x => x.AccessToken).Returns((string)null);

            yield return AsyncTestHelpers.ExecuteTask(async() =>
            {
                // when/then
                // check that invocation throws an exception
                await AsyncTestHelpers.ThrowsAsync<Exception>(() => _qosService.GetSortedQosResultsAsync(null, null));
            });
        }

        [UnityTest]
        public IEnumerator Test_GetSortedQosResults_OrdersResults()
        {
            // given
            const string goodRegion = "goodRegion";
            const string okRegion = "okRegion";
            const string badRegion = "badRegion";
            var mockRegions = new List<string> {goodRegion, okRegion, badRegion};
            var mockServers = new List<QosServer>
            {
                new QosServer(new List<string> {"127.0.0.1:666"}, goodRegion, new List<string> {"relay"}),
                new QosServer(new List<string> {"127.0.0.1:666"}, okRegion, new List<string> {"relay"}),
                new QosServer(new List<string> {"127.0.0.1:666"}, badRegion, new List<string> {"relay"}),
            };
            MockQosServers(mockServers);
            var unsortedQosResults = new List<Internal.QosResult>
            {
                new Internal.QosResult {Region = badRegion, AverageLatencyMs = 666, PacketLossPercent = 420f},
                new Internal.QosResult {Region = okRegion, AverageLatencyMs = 1, PacketLossPercent = 1},
                new Internal.QosResult {Region = goodRegion, AverageLatencyMs = 1, PacketLossPercent = 0}
            };
            var expectedSortedQosResults = new List<IQosResult>
            {
                new QosResult(goodRegion, 1, 0), new QosResult(okRegion, 1, 1), new QosResult(badRegion, 666, 420f)
            };
            _qosRunnerMock.Setup(x => x.MeasureQosAsync(It.IsAny<IList<QosServer>>()))
                .Returns(Task.FromResult(unsortedQosResults));

            yield return AsyncTestHelpers.ExecuteTask(async() =>
            {
                // when
                var sortedResults = await _qosService.GetSortedQosResultsAsync("relay", mockRegions);

                // then
                // check results are sorted as expected
                CollectionAssert.AreEqual(expectedSortedQosResults, sortedResults, new IQosResultComparer());
                // check that regions & service were passed correctly in API client call
                _qosDiscoveryApiClientMock.Verify(x => x.GetServersAsync(
                    It.Is<GetServersRequest>(r => r.Region.SequenceEqual(mockRegions) && r.Service == "relay"),
                    null),
                    Times.Exactly(1));
                _qosRunnerMock.Verify(x => x.MeasureQosAsync(It.IsAny<List<QosServer>>()), Times.Exactly(1));
            });
        }

        [UnityTest]
        public IEnumerator Test_GetSortedQosResults_SendsMetrics_WithLocation()
        {
            // given
            const string bestRegionName = "bestRegion";
            const int bestRegionLatencyMs = 1;
            const float bestRegionPacketLossPercent = 0.01f;

            const string okRegionName = "okRegion";
            const int okRegionLatencyMs = 1;
            const float okRegionPacketLossPercent = 0.1f;

            const string badRegionName = "badRegion";
            const int badRegionLatencyMs = 666;
            const float badRegionPacketLossPercent = 0.42f;

            const string clientCountry = "US";
            const string clientRegion = "CA";
            const string relayServiceName = "relay";

            var mockServers = new List<QosServer>
            {
                new QosServer(new List<string> {"127.0.0.1:666"}, bestRegionName,
                    new List<string> {relayServiceName}),
                new QosServer(new List<string> {"127.0.0.1:666"}, okRegionName,
                    new List<string> {relayServiceName}),
                new QosServer(new List<string> {"127.0.0.1:666"}, badRegionName,
                    new List<string> {relayServiceName}),
            };
            var responseHeaders = new Dictionary<string, string>
            {
                {"X-Client-Country", clientCountry}, {"X-Client-Region", clientRegion}
            };
            MockQosServers(mockServers, responseHeaders);
            var qosResults = new List<Internal.QosResult>
            {
                new Internal.QosResult
                {
                    Region = badRegionName,
                    AverageLatencyMs = badRegionLatencyMs,
                    PacketLossPercent = badRegionPacketLossPercent
                },
                new Internal.QosResult
                {
                    Region = okRegionName,
                    AverageLatencyMs = okRegionLatencyMs,
                    PacketLossPercent = okRegionPacketLossPercent
                },
                new Internal.QosResult
                {
                    Region = bestRegionName,
                    AverageLatencyMs = bestRegionLatencyMs,
                    PacketLossPercent = bestRegionPacketLossPercent
                }
            };
            _qosRunnerMock.Setup(x => x.MeasureQosAsync(It.IsAny<IList<QosServer>>()))
                .Returns(Task.FromResult(qosResults));
            _metricsMock.Setup(m =>
                m.SendHistogramMetric(It.IsAny<string>(), It.IsAny<float>(), It.IsAny<Dictionary<string, string>>()));

            // when
            yield return AsyncTestHelpers.ExecuteTask(_qosService.GetSortedQosResultsAsync(relayServiceName, null));

            // then
            var bestResultExpectedTags = new Dictionary<string, string>
            {
                {"qos_service_name", relayServiceName},
                {"qos_service_region", bestRegionName},
                {"qos_client_country", clientCountry},
                {"qos_client_region", clientRegion},
                {"qos_best_result", "true"} // extra tag to distinguish the "best" result
            };
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_latency_ms", bestRegionLatencyMs, bestResultExpectedTags));
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_packet_loss", bestRegionPacketLossPercent, bestResultExpectedTags));
            var okResultExpectedTags = new Dictionary<string, string>
            {
                {"qos_service_name", relayServiceName},
                {"qos_service_region", okRegionName},
                {"qos_client_country", clientCountry},
                {"qos_client_region", clientRegion}
            };
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_latency_ms", okRegionLatencyMs, okResultExpectedTags));
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_packet_loss", okRegionPacketLossPercent, okResultExpectedTags));
            var badResultExpectedTags = new Dictionary<string, string>
            {
                {"qos_service_name", relayServiceName},
                {"qos_service_region", badRegionName},
                {"qos_client_country", clientCountry},
                {"qos_client_region", clientRegion},
            };
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_latency_ms", badRegionLatencyMs, badResultExpectedTags));
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_packet_loss", badRegionPacketLossPercent, badResultExpectedTags));
        }

        [UnityTest]
        public IEnumerator Test_GetSortedQosResults_SendsMetrics_NoLocation()
        {
            // given
            const string bestRegionName = "bestRegion";
            const int bestRegionLatencyMs = 1;
            const float bestRegionPacketLossPercent = 0.01f;

            const string okRegionName = "okRegion";
            const int okRegionLatencyMs = 1;
            const float okRegionPacketLossPercent = 0.1f;

            const string badRegionName = "badRegion";
            const int badRegionLatencyMs = 666;
            const float badRegionPacketLossPercent = 0.42f;

            const string relayServiceName = "relay";

            var mockServers = new List<QosServer>
            {
                new QosServer(new List<string> {"127.0.0.1:666"}, bestRegionName,
                    new List<string> {relayServiceName}),
                new QosServer(new List<string> {"127.0.0.1:666"}, okRegionName,
                    new List<string> {relayServiceName}),
                new QosServer(new List<string> {"127.0.0.1:666"}, badRegionName,
                    new List<string> {relayServiceName}),
            };
            MockQosServers(mockServers);
            var qosResults = new List<Internal.QosResult>
            {
                new Internal.QosResult
                {
                    Region = badRegionName,
                    AverageLatencyMs = badRegionLatencyMs,
                    PacketLossPercent = badRegionPacketLossPercent
                },
                new Internal.QosResult
                {
                    Region = okRegionName,
                    AverageLatencyMs = okRegionLatencyMs,
                    PacketLossPercent = okRegionPacketLossPercent
                },
                new Internal.QosResult
                {
                    Region = bestRegionName,
                    AverageLatencyMs = bestRegionLatencyMs,
                    PacketLossPercent = bestRegionPacketLossPercent
                }
            };
            _qosRunnerMock.Setup(x => x.MeasureQosAsync(It.IsAny<IList<QosServer>>()))
                .Returns(Task.FromResult(qosResults));
            _metricsMock.Setup(m =>
                m.SendHistogramMetric(It.IsAny<string>(), It.IsAny<float>(), It.IsAny<Dictionary<string, string>>()));

            // when
            yield return AsyncTestHelpers.ExecuteTask(_qosService.GetSortedQosResultsAsync(relayServiceName, null));

            // then
            var bestResultExpectedTags = new Dictionary<string, string>
            {
                {"qos_service_name", relayServiceName},
                {"qos_service_region", bestRegionName},
                {"qos_best_result", "true"} // extra tag to distinguish the "best" result
            };
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_latency_ms", bestRegionLatencyMs, bestResultExpectedTags));
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_packet_loss", bestRegionPacketLossPercent, bestResultExpectedTags));
            var okResultExpectedTags = new Dictionary<string, string>
            {
                {"qos_service_name", relayServiceName}, {"qos_service_region", okRegionName},
            };
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_latency_ms", okRegionLatencyMs, okResultExpectedTags));
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_packet_loss", okRegionPacketLossPercent, okResultExpectedTags));
            var badResultExpectedTags = new Dictionary<string, string>
            {
                {"qos_service_name", relayServiceName}, {"qos_service_region", badRegionName},
            };
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_latency_ms", badRegionLatencyMs, badResultExpectedTags));
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_packet_loss", badRegionPacketLossPercent, badResultExpectedTags));
        }

        [UnityTest]
        public IEnumerator Test_GetSortedQosResults_ReturnsEmptyListIfNoServersFound()
        {
            // given
            var qosResponseBody = new QosServersResponseBody(new QosServersList(new List<QosServer>()));
            var empty = new Response<QosServersResponseBody>(MockResponse(), qosResponseBody);
            _qosDiscoveryApiClientMock
                .Setup(x => x.GetServersAsync(It.IsAny<GetServersRequest>(), null))
                .Returns(Task.FromResult(empty));

            yield return AsyncTestHelpers.ExecuteTask(async() =>
            {
                // when
                var results = await _qosService.GetSortedQosResultsAsync(null, null);

                // then
                CollectionAssert.IsEmpty(results);
            });
        }

        [UnityTest]
        public IEnumerator Test_GetSortedRelayQosResults_OrdersResults()
        {
            // given
            const string goodRegion = "goodRegion";
            const string badRegion = "badRegion";
            const string worseRegion = "worseRegion";
            var mockRegions = new List<string> {goodRegion, badRegion, worseRegion};
            var mockServers = new List<QosServiceServer>
            {
                new QosServiceServer(new List<string> {"127.0.0.1:666"}, goodRegion,
                    new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdRelay}}
                    }),
                new QosServiceServer(new List<string> {"127.0.0.1:666"}, badRegion,
                    new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdRelay}}
                    }),
                new QosServiceServer(new List<string> {"127.0.0.1:666"}, worseRegion,
                    new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdRelay}}
                    }),
            };
            MockServiceQosServers(mockServers);
            var unsortedQosResults = new List<QosAnnotatedResult>
            {
                new QosAnnotatedResult
                {
                    Region = worseRegion,
                    AverageLatencyMs = int.MaxValue,
                    PacketLossPercent = float.MaxValue,
                    Annotations = new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdRelay}}
                    }
                },
                new QosAnnotatedResult
                {
                    Region = badRegion,
                    AverageLatencyMs = 1,
                    PacketLossPercent = 1,
                    Annotations = new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdRelay}}
                    }
                },
                new QosAnnotatedResult
                {
                    Region = goodRegion,
                    AverageLatencyMs = 1,
                    PacketLossPercent = 0,
                    Annotations = new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdRelay}}
                    }
                }
            };
            var expectedSortedQosResults = new List<IQosResult> {new QosResult(goodRegion, 1, 0)};
            _qosRunnerMock.Setup(x => x.MeasureQosAsync(It.IsAny<IList<QosServiceServer>>()))
                .Returns(Task.FromResult(unsortedQosResults));

            yield return AsyncTestHelpers.ExecuteTask(async() =>
            {
                // when
                var sortedResults = await _qosService.GetSortedRelayQosResultsAsync(mockRegions);

                // then
                // check results are sorted as expected
                CollectionAssert.AreEqual(expectedSortedQosResults, sortedResults, new IQosResultComparer());
                // check that regions & service were passed correctly in API client call
                _qosDiscoveryApiClientMock.Verify(x => x.GetServiceServersAsync(
                    It.Is<GetServiceServersRequest>(r =>
                        r.Region.SequenceEqual(mockRegions) &&
                        r.ServiceId == GetServiceServersRequest.ServiceIdRelay), null),
                    Times.Exactly(1));
                _qosRunnerMock.Verify(x => x.MeasureQosAsync(It.IsAny<List<QosServiceServer>>()), Times.Exactly(1));
            });
        }

        [UnityTest]
        public IEnumerator Test_GetSortedRelayQosResults_SendsMetrics_WithLocation()
        {
            // given
            const string bestRegionName = "bestRegion";
            const int bestRegionLatencyMs = 1;
            const float bestRegionPacketLossPercent = 0.01f;

            const string okRegionName = "okRegion";
            const int okRegionLatencyMs = 1;
            const float okRegionPacketLossPercent = 0.1f;

            const string badRegionName = "badRegion";
            const int badRegionLatencyMs = 666;
            const float badRegionPacketLossPercent = 0.42f;

            const string clientCountry = "US";
            const string clientRegion = "CA";

            var mockServers = new List<QosServiceServer>
            {
                new QosServiceServer(new List<string> {"127.0.0.1:666"}, bestRegionName,
                    new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdRelay}}
                    }),
                new QosServiceServer(new List<string> {"127.0.0.1:666"}, okRegionName,
                    new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdRelay}}
                    }),
                new QosServiceServer(new List<string> {"127.0.0.1:666"}, badRegionName,
                    new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdRelay}}
                    }),
            };
            var responseHeaders = new Dictionary<string, string>
            {
                {"X-Client-Country", clientCountry}, {"X-Client-Region", clientRegion}
            };
            MockServiceQosServers(mockServers, responseHeaders);
            var qosResults = new List<QosAnnotatedResult>
            {
                new QosAnnotatedResult
                {
                    Region = badRegionName,
                    AverageLatencyMs = badRegionLatencyMs,
                    PacketLossPercent = badRegionPacketLossPercent,
                    Annotations = new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdRelay}}
                    }
                },
                new QosAnnotatedResult
                {
                    Region = okRegionName,
                    AverageLatencyMs = okRegionLatencyMs,
                    PacketLossPercent = okRegionPacketLossPercent,
                    Annotations = new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdRelay}}
                    }
                },
                new QosAnnotatedResult
                {
                    Region = bestRegionName,
                    AverageLatencyMs = bestRegionLatencyMs,
                    PacketLossPercent = bestRegionPacketLossPercent,
                    Annotations = new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdRelay}}
                    }
                }
            };
            _qosRunnerMock.Setup(x => x.MeasureQosAsync(It.IsAny<IList<QosServiceServer>>()))
                .Returns(Task.FromResult(qosResults));
            _metricsMock.Setup(m =>
                m.SendHistogramMetric(It.IsAny<string>(), It.IsAny<float>(), It.IsAny<Dictionary<string, string>>()));

            // when
            yield return AsyncTestHelpers.ExecuteTask(_qosService.GetSortedRelayQosResultsAsync(null));

            // then
            var bestResultExpectedTags = new Dictionary<string, string>
            {
                {"qos_service_name", GetServiceServersRequest.ServiceIdRelay},
                {"qos_service_region", bestRegionName},
                {"qos_client_country", clientCountry},
                {"qos_client_region", clientRegion},
                {"qos_best_result", "true"} // extra tag to distinguish the "best" result
            };
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_latency_ms", bestRegionLatencyMs, bestResultExpectedTags));
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_packet_loss", bestRegionPacketLossPercent, bestResultExpectedTags));
            var okResultExpectedTags = new Dictionary<string, string>
            {
                {"qos_service_name", GetServiceServersRequest.ServiceIdRelay},
                {"qos_service_region", okRegionName},
                {"qos_client_country", clientCountry},
                {"qos_client_region", clientRegion}
            };
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_latency_ms", okRegionLatencyMs, okResultExpectedTags));
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_packet_loss", okRegionPacketLossPercent, okResultExpectedTags));
            var badResultExpectedTags = new Dictionary<string, string>
            {
                {"qos_service_name", GetServiceServersRequest.ServiceIdRelay},
                {"qos_service_region", badRegionName},
                {"qos_client_country", clientCountry},
                {"qos_client_region", clientRegion},
            };
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_latency_ms", badRegionLatencyMs, badResultExpectedTags));
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_packet_loss", badRegionPacketLossPercent, badResultExpectedTags));
        }

        [UnityTest]
        public IEnumerator Test_GetSortedRelayQosResults_SendsMetrics_NoLocation()
        {
            // given
            const string bestRegionName = "bestRegion";
            const int bestRegionLatencyMs = 1;
            const float bestRegionPacketLossPercent = 0.01f;

            const string okRegionName = "okRegion";
            const int okRegionLatencyMs = 1;
            const float okRegionPacketLossPercent = 0.1f;

            const string badRegionName = "badRegion";
            const int badRegionLatencyMs = 666;
            const float badRegionPacketLossPercent = 0.42f;

            var mockServers = new List<QosServiceServer>
            {
                new QosServiceServer(new List<string> {"127.0.0.1:666"}, bestRegionName,
                    new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdRelay}}
                    }),
                new QosServiceServer(new List<string> {"127.0.0.1:666"}, okRegionName,
                    new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdRelay}}
                    }),
                new QosServiceServer(new List<string> {"127.0.0.1:666"}, badRegionName,
                    new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdRelay}}
                    }),
            };
            MockServiceQosServers(mockServers);
            var qosResults = new List<QosAnnotatedResult>
            {
                new QosAnnotatedResult
                {
                    Region = badRegionName,
                    AverageLatencyMs = badRegionLatencyMs,
                    PacketLossPercent = badRegionPacketLossPercent,
                    Annotations = new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdRelay}}
                    }
                },
                new QosAnnotatedResult
                {
                    Region = okRegionName,
                    AverageLatencyMs = okRegionLatencyMs,
                    PacketLossPercent = okRegionPacketLossPercent,
                    Annotations = new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdRelay}}
                    }
                },
                new QosAnnotatedResult
                {
                    Region = bestRegionName,
                    AverageLatencyMs = bestRegionLatencyMs,
                    PacketLossPercent = bestRegionPacketLossPercent,
                    Annotations = new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdRelay}}
                    }
                }
            };
            _qosRunnerMock.Setup(x => x.MeasureQosAsync(It.IsAny<IList<QosServiceServer>>()))
                .Returns(Task.FromResult(qosResults));
            _metricsMock.Setup(m =>
                m.SendHistogramMetric(It.IsAny<string>(), It.IsAny<float>(), It.IsAny<Dictionary<string, string>>()));

            // when
            yield return AsyncTestHelpers.ExecuteTask(_qosService.GetSortedRelayQosResultsAsync(null));

            // then
            var bestResultExpectedTags = new Dictionary<string, string>
            {
                {"qos_service_name", GetServiceServersRequest.ServiceIdRelay},
                {"qos_service_region", bestRegionName},
                {"qos_best_result", "true"} // extra tag to distinguish the "best" result
            };
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_latency_ms", bestRegionLatencyMs, bestResultExpectedTags));
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_packet_loss", bestRegionPacketLossPercent, bestResultExpectedTags));
            var okResultExpectedTags = new Dictionary<string, string>
            {
                {"qos_service_name", GetServiceServersRequest.ServiceIdRelay}, {"qos_service_region", okRegionName},
            };
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_latency_ms", okRegionLatencyMs, okResultExpectedTags));
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_packet_loss", okRegionPacketLossPercent, okResultExpectedTags));
            var badResultExpectedTags = new Dictionary<string, string>
            {
                {"qos_service_name", GetServiceServersRequest.ServiceIdRelay},
                {"qos_service_region", badRegionName},
            };
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_latency_ms", badRegionLatencyMs, badResultExpectedTags));
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_packet_loss", badRegionPacketLossPercent, badResultExpectedTags));
        }

        [UnityTest]
        public IEnumerator Test_GetSortedRelayQosResults_ReturnsEmptyListIfNoServersFound()
        {
            // given
            var qosResponseBody =
                new QosServiceServersResponseBody(new QosServiceServersList(new List<QosServiceServer>()));
            var empty = new Response<QosServiceServersResponseBody>(MockResponse(), qosResponseBody);
            _qosDiscoveryApiClientMock
                .Setup(x => x.GetServiceServersAsync(It.IsAny<GetServiceServersRequest>(), null))
                .Returns(Task.FromResult(empty));

            yield return AsyncTestHelpers.ExecuteTask(async() =>
            {
                // when
                var results = await _qosService.GetSortedRelayQosResultsAsync(null);

                // then
                CollectionAssert.IsEmpty(results);
            });
        }

        [UnityTest]
        public IEnumerator Test_GetAllServersAsync_OK()
        {
            yield return AsyncTestHelpers.ExecuteTask(async() =>
            {
                // given
                var servers = new List<V2.Models.QosServer>() {new V2.Models.QosServer(new List<string>() {"hello"}, new QosServerAnnotations())};
                var body = new V2.Models.QosServersResponseBody(new V2.Models.QosServersList(servers));
                var response = new V2.Response<V2.Models.QosServersResponseBody>(MockResponseV2(), body);
                _qosDiscoveryApiClientMockV2.Setup(x => x.GetAllServersAsync(It.IsAny<GetAllServersRequest>(), It.IsAny<V2.Configuration>()))
                    .Returns(Task.FromResult(response));

                // when
                var got = await _qosService.GetAllServersAsync();

                // then
                CollectionAssert.AreEqual(servers, got);
            });
        }

        [UnityTest]
        public IEnumerator Test_GetAllServersAsync_HandlesETag()
        {
            yield return AsyncTestHelpers.ExecuteTask(async() =>
            {
                // given
                var etag = "12345";
                var servers =
                    new List<V2.Models.QosServer>() {new V2.Models.QosServer(new List<string>() {"hello"}, new QosServerAnnotations())};
                var body = new V2.Models.QosServersResponseBody(new V2.Models.QosServersList(servers));
                var headers = new Dictionary<string, string> {{"ETag", etag}};
                var response = new V2.Response<V2.Models.QosServersResponseBody>(MockResponseV2(headers), body);
                var resp304 = MockResponseV2(null, (long)HttpStatusCode.NotModified);

                // fake etag behavior.
                _qosDiscoveryApiClientMockV2.Setup(x => x.GetAllServersAsync(It.IsAny<GetAllServersRequest>(),
                    It.Is<V2.Configuration>(
                        c => !c.Headers.ContainsKey("If-None-Match") || c.Headers["If-None-Match"] != etag))).Returns(Task.FromResult(response));
                _qosDiscoveryApiClientMockV2.Setup(x => x.GetAllServersAsync(It.IsAny<GetAllServersRequest>(),
                    It.Is<V2.Configuration>(
                        c => c.Headers.ContainsKey("If-None-Match") && c.Headers["If-None-Match"] == etag))).Throws(new V2.Http.HttpException(resp304));

                // when
                _ = await _qosService.GetAllServersAsync();
                var got = await _qosService.GetAllServersAsync();  // the 2nd call is expected to set If-None-Match to etag and handle the 304 response.

                // then
                CollectionAssert.AreEqual(servers, got);

                _qosDiscoveryApiClientMockV2.Verify(x => x.GetAllServersAsync(It.IsAny<GetAllServersRequest>(),
                    It.Is<V2.Configuration>(
                        c => !c.Headers.ContainsKey("If-None-Match") || c.Headers["If-None-Match"] != etag)), Times.Once());
                _qosDiscoveryApiClientMockV2.Verify(x => x.GetAllServersAsync(It.IsAny<GetAllServersRequest>(),
                    It.Is<V2.Configuration>(
                        c => c.Headers.ContainsKey("If-None-Match") && c.Headers["If-None-Match"] == etag)), Times.Once());
            });
        }

        [UnityTest]
        public IEnumerator Test_GetQosResultsAsync_ForwardsResultsFromRunner()
        {
            yield return AsyncTestHelpers.ExecuteTask(async() =>
            {
                // given
                var server = new V2.Models.QosServer(new List<string> {"127.0.0.1:22222"},
                    new QosServerAnnotations(relayRegionId: new List<string> {"us-east1"}));
                var measurements =
                    new BaselibQosRunner.QosMeasurementImpl(averageLatencyMs: 111, packetLossPercent: 222);
                var expectedList = new List<(V2.Models.QosServer, IQosMeasurements)> {(server, measurements)};
                var servers = expectedList.ConvertAll(t => t.Item1);

                // stubbing
                _qosRunnerMock.Setup(x => x.MeasureQosV2Async(It.IsAny<IList<V2.Models.QosServer>>()))
                    .Returns(Task.FromResult(expectedList));

                // when
                var got = await _qosService.GetQosResultsAsync(servers);

                // then
                CollectionAssert.AreEqual(expectedList, got);
            });
        }

        [UnityTest]
        public IEnumerator Test_GetQosResultsAsync_SendsMetrics()
        {
            yield return AsyncTestHelpers.ExecuteTask(async() =>
            {
                // given
                var regionName = "us-east1";
                var clientCountry = "Germany";
                var clientRegion = "Berlin";
                var server = new V2.Models.QosServer(new List<string> {"127.0.0.1:22222"},
                    new QosServerAnnotations(relayRegionId: new List<string> {regionName}));
                var measurements =
                    new BaselibQosRunner.QosMeasurementImpl(averageLatencyMs: 111, packetLossPercent: 222);
                var expectedList = new List<(V2.Models.QosServer, IQosMeasurements)> {(server, measurements)};
                var servers = expectedList.ConvertAll(t => t.Item1);
                var headers = new Dictionary<string, string>
                {
                    {"X-Client-Country", clientCountry}, {"X-Client-Region", clientRegion},
                };
                var response = new V2.Response<V2.Models.QosServersResponseBody>(MockResponseV2(headers),
                    new V2.Models.QosServersResponseBody(
                        new V2.Models.QosServersList(new List<V2.Models.QosServer>())));

                // stubbing
                _qosRunnerMock.Setup(x => x.MeasureQosV2Async(It.IsAny<IList<V2.Models.QosServer>>()))
                    .Returns(Task.FromResult(expectedList));
                _qosDiscoveryApiClientMockV2.Setup(x => x.GetAllServersAsync(It.IsAny<GetAllServersRequest>(), It.IsAny<V2.Configuration>()))
                    .Returns(Task.FromResult(response));

                // when
                await _qosService.GetAllServersAsync(); // should save the country and region from the request.
                var got = await _qosService.GetQosResultsAsync(servers);

                // then
                // NOTE(ptrottier): if the order of these tags change, the test will fail, but the behavior is OK. This is a limitation of the mock check.
                var expectedTags = new Dictionary<string, string>
                {
                    {"qos_service_name", "relay"},
                    {"qos_service_region", regionName},
                    {"qos_client_country", clientCountry},
                    {"qos_client_region", clientRegion},
                    {"qos_best_result", "true"}
                };
                _metricsMock.Verify(m =>
                    m.SendHistogramMetric("qos_result_latency_ms", measurements.AverageLatencyMs, expectedTags));
                _metricsMock.Verify(m =>
                    m.SendHistogramMetric("qos_result_packet_loss", measurements.PacketLossPercent, expectedTags));
            });
        }

        [UnityTest]
        public IEnumerator Test_GetSortedMultiplayQosResults_OrdersResults()
        {
            // given
            const string goodRegion = "goodRegion";
            const string okRegion = "okRegion";
            const string badRegion = "badRegion";
            var mockFleet = new List<string> {"someFleetID"};
            var mockServers = new List<QosServiceServer>
            {
                new QosServiceServer(new List<string> {"127.0.0.1:666"}, goodRegion,
                    new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdMultiplay}}
                    }),
                new QosServiceServer(new List<string> {"127.0.0.1:666"}, okRegion,
                    new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdMultiplay}}
                    }),
                new QosServiceServer(new List<string> {"127.0.0.1:666"}, badRegion,
                    new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdMultiplay}}
                    }),
            };
            MockServiceQosServers(mockServers);
            var unsortedQosResults = new List<QosAnnotatedResult>
            {
                new QosAnnotatedResult
                {
                    Region = badRegion,
                    AverageLatencyMs = int.MaxValue,
                    PacketLossPercent = float.MaxValue,
                    Annotations = new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdMultiplay}}
                    }
                },
                new QosAnnotatedResult
                {
                    Region = okRegion,
                    AverageLatencyMs = 1,
                    PacketLossPercent = 1,
                    Annotations = new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdMultiplay}}
                    }
                },
                new QosAnnotatedResult
                {
                    Region = goodRegion,
                    AverageLatencyMs = 1,
                    PacketLossPercent = 0,
                    Annotations = new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdMultiplay}}
                    }
                },
                new QosAnnotatedResult
                {
                    Region = okRegion,
                    AverageLatencyMs = 5,
                    PacketLossPercent = 0.5f,
                    Annotations = new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdMultiplay}}
                    }
                },
                new QosAnnotatedResult
                {
                    Region = goodRegion,
                    AverageLatencyMs = 3,
                    PacketLossPercent = 0,
                    Annotations = new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdMultiplay}}
                    }
                }
            };
            var expectedSortedQosResults = new List<IQosResult>
            {
                new QosResult(goodRegion, 2, 0), new QosResult(okRegion, 5, 0.5f)
            };
            _qosRunnerMock.Setup(x => x.MeasureQosAsync(It.IsAny<IList<QosServiceServer>>()))
                .Returns(Task.FromResult(unsortedQosResults));

            yield return AsyncTestHelpers.ExecuteTask(async() =>
            {
                // when
                var sortedResults = await _qosService.GetSortedMultiplayQosResultsAsync(mockFleet);

                // then
                // check results are sorted as expected
                CollectionAssert.AreEqual(expectedSortedQosResults, sortedResults, new IQosResultComparer());
                // check that regions & service were passed correctly in API client call
                _qosDiscoveryApiClientMock.Verify(x => x.GetServiceServersAsync(
                    It.Is<GetServiceServersRequest>(r =>
                        r.Fleet.Equals(mockFleet) && r.ServiceId == GetServiceServersRequest.ServiceIdMultiplay),
                    null),
                    Times.Exactly(1));
                _qosRunnerMock.Verify(x => x.MeasureQosAsync(It.IsAny<List<QosServiceServer>>()), Times.Exactly(1));
            });
        }

        [UnityTest]
        public IEnumerator Test_GetSortedMultiplayQosResults_SendsMetrics_WithLocation()
        {
            // given
            const string bestRegionName = "bestRegion";
            const int bestRegionLatencyMs = 1;
            const float bestRegionPacketLossPercent = 0.01f;

            const string okRegionName = "okRegion";
            const int okRegionLatencyMs = 1;
            const float okRegionPacketLossPercent = 0.1f;

            const string badRegionName = "badRegion";
            const int badRegionLatencyMs = 666;
            const float badRegionPacketLossPercent = 0.42f;

            const string clientCountry = "US";
            const string clientRegion = "CA";

            var mockServers = new List<QosServiceServer>
            {
                new QosServiceServer(new List<string> {"127.0.0.1:666"}, bestRegionName,
                    new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdMultiplay}}
                    }),
                new QosServiceServer(new List<string> {"127.0.0.1:666"}, okRegionName,
                    new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdMultiplay}}
                    }),
                new QosServiceServer(new List<string> {"127.0.0.1:666"}, badRegionName,
                    new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdMultiplay}}
                    }),
            };
            var responseHeaders = new Dictionary<string, string>
            {
                {"X-Client-Country", clientCountry}, {"X-Client-Region", clientRegion}
            };
            MockServiceQosServers(mockServers, responseHeaders);
            var qosResults = new List<QosAnnotatedResult>
            {
                new QosAnnotatedResult
                {
                    Region = badRegionName,
                    AverageLatencyMs = badRegionLatencyMs,
                    PacketLossPercent = badRegionPacketLossPercent,
                    Annotations = new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdMultiplay}}
                    }
                },
                new QosAnnotatedResult
                {
                    Region = okRegionName,
                    AverageLatencyMs = okRegionLatencyMs,
                    PacketLossPercent = okRegionPacketLossPercent,
                    Annotations = new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdMultiplay}}
                    }
                },
                new QosAnnotatedResult
                {
                    Region = bestRegionName,
                    AverageLatencyMs = bestRegionLatencyMs,
                    PacketLossPercent = bestRegionPacketLossPercent,
                    Annotations = new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdMultiplay}}
                    }
                }
            };
            _qosRunnerMock.Setup(x => x.MeasureQosAsync(It.IsAny<IList<QosServiceServer>>()))
                .Returns(Task.FromResult(qosResults));
            _metricsMock.Setup(m =>
                m.SendHistogramMetric(It.IsAny<string>(), It.IsAny<float>(), It.IsAny<Dictionary<string, string>>()));

            // when
            yield return AsyncTestHelpers.ExecuteTask(
                _qosService.GetSortedMultiplayQosResultsAsync(new List<string> {"someFleetID"}));

            // then
            var bestResultExpectedTags = new Dictionary<string, string>
            {
                {"qos_service_name", GetServiceServersRequest.ServiceIdMultiplay},
                {"qos_service_region", bestRegionName},
                {"qos_client_country", clientCountry},
                {"qos_client_region", clientRegion},
                {"qos_best_result", "true"} // extra tag to distinguish the "best" result
            };
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_latency_ms", bestRegionLatencyMs, bestResultExpectedTags));
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_packet_loss", bestRegionPacketLossPercent, bestResultExpectedTags));
            var okResultExpectedTags = new Dictionary<string, string>
            {
                {"qos_service_name", GetServiceServersRequest.ServiceIdMultiplay},
                {"qos_service_region", okRegionName},
                {"qos_client_country", clientCountry},
                {"qos_client_region", clientRegion}
            };
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_latency_ms", okRegionLatencyMs, okResultExpectedTags));
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_packet_loss", okRegionPacketLossPercent, okResultExpectedTags));
            var badResultExpectedTags = new Dictionary<string, string>
            {
                {"qos_service_name", GetServiceServersRequest.ServiceIdMultiplay},
                {"qos_service_region", badRegionName},
                {"qos_client_country", clientCountry},
                {"qos_client_region", clientRegion},
            };
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_latency_ms", badRegionLatencyMs, badResultExpectedTags));
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_packet_loss", badRegionPacketLossPercent, badResultExpectedTags));
        }

        [UnityTest]
        public IEnumerator Test_GetSortedMultiplayQosResults_SendsMetrics_NoLocation()
        {
            // given
            const string bestRegionName = "bestRegion";
            const int bestRegionLatencyMs = 1;
            const float bestRegionPacketLossPercent = 0.01f;

            const string okRegionName = "okRegion";
            const int okRegionLatencyMs = 1;
            const float okRegionPacketLossPercent = 0.1f;

            const string badRegionName = "badRegion";
            const int badRegionLatencyMs = 666;
            const float badRegionPacketLossPercent = 0.42f;

            var mockServers = new List<QosServiceServer>
            {
                new QosServiceServer(new List<string> {"127.0.0.1:666"}, bestRegionName,
                    new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdMultiplay}}
                    }),
                new QosServiceServer(new List<string> {"127.0.0.1:666"}, okRegionName,
                    new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdMultiplay}}
                    }),
                new QosServiceServer(new List<string> {"127.0.0.1:666"}, badRegionName,
                    new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdMultiplay}}
                    }),
            };
            MockServiceQosServers(mockServers);
            var qosResults = new List<QosAnnotatedResult>
            {
                new QosAnnotatedResult
                {
                    Region = badRegionName,
                    AverageLatencyMs = badRegionLatencyMs,
                    PacketLossPercent = badRegionPacketLossPercent,
                    Annotations = new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdMultiplay}}
                    }
                },
                new QosAnnotatedResult
                {
                    Region = okRegionName,
                    AverageLatencyMs = okRegionLatencyMs,
                    PacketLossPercent = okRegionPacketLossPercent,
                    Annotations = new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdMultiplay}}
                    }
                },
                new QosAnnotatedResult
                {
                    Region = bestRegionName,
                    AverageLatencyMs = bestRegionLatencyMs,
                    PacketLossPercent = bestRegionPacketLossPercent,
                    Annotations = new Dictionary<string, List<string>>
                    {
                        {"service", new List<string> {GetServiceServersRequest.ServiceIdMultiplay}}
                    }
                }
            };
            _qosRunnerMock.Setup(x => x.MeasureQosAsync(It.IsAny<IList<QosServiceServer>>()))
                .Returns(Task.FromResult(qosResults));
            _metricsMock.Setup(m =>
                m.SendHistogramMetric(It.IsAny<string>(), It.IsAny<float>(), It.IsAny<Dictionary<string, string>>()));

            // when
            yield return AsyncTestHelpers.ExecuteTask(
                _qosService.GetSortedMultiplayQosResultsAsync(new List<string> {"someFleetID"}));

            // then
            var bestResultExpectedTags = new Dictionary<string, string>
            {
                {"qos_service_name", GetServiceServersRequest.ServiceIdMultiplay},
                {"qos_service_region", bestRegionName},
                {"qos_best_result", "true"} // extra tag to distinguish the "best" result
            };
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_latency_ms", bestRegionLatencyMs, bestResultExpectedTags));
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_packet_loss", bestRegionPacketLossPercent, bestResultExpectedTags));
            var okResultExpectedTags = new Dictionary<string, string>
            {
                {"qos_service_name", GetServiceServersRequest.ServiceIdMultiplay},
                {"qos_service_region", okRegionName},
            };
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_latency_ms", okRegionLatencyMs, okResultExpectedTags));
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_packet_loss", okRegionPacketLossPercent, okResultExpectedTags));
            var badResultExpectedTags = new Dictionary<string, string>
            {
                {"qos_service_name", GetServiceServersRequest.ServiceIdMultiplay},
                {"qos_service_region", badRegionName},
            };
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_latency_ms", badRegionLatencyMs, badResultExpectedTags));
            _metricsMock.Verify(m =>
                m.SendHistogramMetric("qos_result_packet_loss", badRegionPacketLossPercent, badResultExpectedTags));
        }

        [UnityTest]
        public IEnumerator Test_GetSortedMultiplayQosResults_ReturnsEmptyListIfNoServersFound()
        {
            // given
            var qosResponseBody =
                new QosServiceServersResponseBody(new QosServiceServersList(new List<QosServiceServer>()));
            var empty = new Response<QosServiceServersResponseBody>(MockResponse(), qosResponseBody);
            _qosDiscoveryApiClientMock
                .Setup(x => x.GetServiceServersAsync(It.IsAny<GetServiceServersRequest>(), null))
                .Returns(Task.FromResult(empty));

            yield return AsyncTestHelpers.ExecuteTask(async() =>
            {
                // when
                var results = await _qosService.GetSortedMultiplayQosResultsAsync(new List<string> {"someFleetID"});

                // then
                CollectionAssert.IsEmpty(results);
            });
        }

        HttpClientResponse MockResponse(Dictionary<string, string> headers = null)
        {
            var realHeaders = headers ?? new Dictionary<string, string>();
            return new HttpClientResponse(realHeaders, 200, false, false, new byte[] {}, "");
        }

        V2.Http.HttpClientResponse MockResponseV2(Dictionary<string, string> headers = null, long statusCode = 200)
        {
            var realHeaders = headers ?? new Dictionary<string, string>();
            return new V2.Http.HttpClientResponse(realHeaders, statusCode, false, false, new byte[] {}, "");
        }

        void MockQosServers(List<QosServer> servers, Dictionary<string, string> headers = null)
        {
            var qosResponseBody = new QosServersResponseBody(new QosServersList(servers));
            var mockedQosServers = new Response<QosServersResponseBody>(MockResponse(headers), qosResponseBody);
            _qosDiscoveryApiClientMock
                .Setup(x => x.GetServersAsync(It.IsAny<GetServersRequest>(), null))
                .Returns(Task.FromResult(mockedQosServers));
        }

        void MockServiceQosServers(List<QosServiceServer> servers, Dictionary<string, string> headers = null)
        {
            var qosResponseBody = new QosServiceServersResponseBody(new QosServiceServersList(servers));
            var mockedQosServers = new Response<QosServiceServersResponseBody>(MockResponse(headers), qosResponseBody);
            _qosDiscoveryApiClientMock
                .Setup(x => x.GetServiceServersAsync(It.IsAny<GetServiceServersRequest>(), null))
                .Returns(Task.FromResult(mockedQosServers));
        }

#else // UGS_QOS_SUPPORTED
        [UnityTest]
        public IEnumerator Test_GetSortedQoSResults_ThrowsIfNoUnsupportedEditor()
        {
            // given
            // override call in Setup()
            _accessTokenMock.Setup(x => x.AccessToken).Returns((string)null);

            yield return AsyncTestHelpers.ExecuteTask(async() =>
            {
                // when/then
                // check that invocation throws an exception
                await AsyncTestHelpers.ThrowsAsync<UnsupportedEditorVersionException>(() => _qosService.GetSortedQosResultsAsync(null, null));
            });
        }

#endif // UGS_QOS_SUPPORTED
#if UNITY_WEBGL
        [UnityTest]
        public IEnumerator Test_GetSortedQoSResults_ThrowsIfWebGL()
        {
            // given
            // override call in Setup()
            _accessTokenMock.Setup(x => x.AccessToken).Returns((string)null);

            yield return AsyncTestHelpers.ExecuteTask(async() =>
            {
                // when/then
                // check that invocation throws an exception
                await AsyncTestHelpers.ThrowsAsync<PlatformNotSupportedException>(() => _qosService.GetSortedQosResultsAsync(null, null));
            });
        }

#endif // UNITY_WEBGL
#else // NUGET_MOQ_AVAILABLE && UNITY_EDITOR
        // We need at least one Runtime test that doesn't use moq so that mobile test jobs in CI don't fail with
        // "No tests have been selected to run." or a compilation error.
        [Test]
        public void Test_Dummy()
        {
            Assert.Pass();
        }

#endif // NUGET_MOQ_AVAILABLE && UNITY_EDITOR
    }
}
