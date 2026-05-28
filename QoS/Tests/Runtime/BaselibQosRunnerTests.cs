using NUnit.Framework;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
#if NUGET_MOQ_AVAILABLE && UNITY_EDITOR
using Moq;
using Unity.Collections.LowLevel.Unsafe;
#endif
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe.NotBurstCompatible;
using RocketScience.Networking.Qos;
using RocketScience.Services.Qos.Models;
using RocketScience.Services.Qos.QosDiscovery;
using RocketScience.Services.Qos.Runner;
using UnityEngine;
using UnityEngine.TestTools;
using QosServer = RocketScience.Services.Qos.Models.QosServer;
using Task = System.Threading.Tasks.Task;

namespace RocketScience.Services.Qos.Tests
{
    class BaselibQosRunnerTests
    {
#if NUGET_MOQ_AVAILABLE && UNITY_EDITOR && UGS_QOS_SUPPORTED
        IList<QosServer> mockServers = new List<QosServer>
        {
            new QosServer(new List<string> {"127.0.0.1:666"}, "region1", new List<string> {"relay"}),
            new QosServer(new List<string> {"127.0.0.1:777"}, "region2", new List<string> {"relay"}),
            new QosServer(new List<string> {"127.0.0.1:888"}, "region3", new List<string> {"relay"}),
        };

        static Dictionary<string, List<string>> mockAnnotation = new Dictionary<string, List<string>> {{"service", new List<string> {GetServiceServersRequest.ServiceIdRelay}}};
        IList<QosServiceServer> mockServiceServers = new List<QosServiceServer>
        {
            new QosServiceServer(new List<string> {"127.0.0.1:666"}, "region1", mockAnnotation),
            new QosServiceServer(new List<string> {"127.0.0.1:777"}, "region2", mockAnnotation),
            new QosServiceServer(new List<string> {"127.0.0.1:888"}, "region3", mockAnnotation),
        };

        Dictionary<string, IPAddress> mockHostIpMap = new Dictionary<string, IPAddress>()
        {
            {"fancy-qos-hostname-us-east1.example.com", IPAddress.Parse("10.0.0.1")},
            {"its-ya-boi.example.com", IPAddress.Parse("10.0.0.2")},
        };

        private class QosComparer : IComparer
        {
            // Mathf.Epsilon is too precise unfortunately
            private readonly float kEpsilon = 0.00001f;

            public int Compare(float x, float y) => Mathf.Abs(x - y) > kEpsilon ? 1 : 0;

            public int Compare(QosAnnotatedResult x, QosAnnotatedResult y)
            {
                if (Compare(x.PacketLossPercent, y.PacketLossPercent) == 0 &&
                    x.AverageLatencyMs == y.AverageLatencyMs &&
                    x.Region.Equals(y.Region) &&
                    x.Annotations.Equals(y.Annotations))
                    return 0;
                return 1;
            }

            public int Compare((V2.Models.QosServer, IQosMeasurements) t1, (V2.Models.QosServer, IQosMeasurements) t2)
            {
                if (t1.Item1 != t2.Item1)
                    return 1;
                if (Compare(t1.Item2.AverageLatencyMs, t2.Item2.AverageLatencyMs) == 1)
                    return 1;
                if (Compare(t1.Item2.PacketLossPercent, t2.Item2.PacketLossPercent) == 1)
                    return 1;

                return 0;
            }

            public int Compare(Internal.QosResult x, Internal.QosResult y)
            {
                if (Compare(x.PacketLossPercent, y.PacketLossPercent) == 0 &&
                    x.AverageLatencyMs == y.AverageLatencyMs &&
                    x.Region.Equals(y.Region))
                    return 0;
                return 1;
            }

            public int Compare(object x, object y)
            {
                if (x is Internal.QosResult && y is Internal.QosResult)
                    return Compare((Internal.QosResult)x, (Internal.QosResult)y);

                if (x is QosAnnotatedResult && y is QosAnnotatedResult)
                    return Compare((QosAnnotatedResult)x, (QosAnnotatedResult)y);

                if (x is ValueTuple<V2.Models.QosServer, IQosMeasurements> && y is ValueTuple<V2.Models.QosServer, IQosMeasurements>)
                    return Compare(((V2.Models.QosServer, IQosMeasurements))x, ((V2.Models.QosServer, IQosMeasurements))y);

                throw new NotImplementedException();
            }
        }

        NativeArray<InternalQosResult> _nativeMockResults;

        Mock<IQosJob> _mockQosJob;
        IList<UcgQosServer> _calledServers;

        IQosRunner _qosRunner;

        [SetUp]
        public void Setup()
        {
            _mockQosJob = new Mock<IQosJob>();
            var mockResults = new[]
            {
                new InternalQosResult {RequestsSent = 10, ResponsesReceived = 10},
                new InternalQosResult {RequestsSent = 10, ResponsesReceived = 9},
                new InternalQosResult {RequestsSent = 10, ResponsesReceived = 6}
            };
            mockResults[0].AddAggregateLatency(53 * 10);
            mockResults[1].AddAggregateLatency(94 * 9);
            mockResults[2].AddAggregateLatency(287 * 6);
            _nativeMockResults = new NativeArray<InternalQosResult>(mockResults, Allocator.Persistent);
            _mockQosJob.SetupGet(x => x.QosResults).Returns(_nativeMockResults);

            QosJobProvider mockQosJobProvider = (servers, _) =>
            {
                _calledServers = servers;
                return _mockQosJob.Object;
            };
            DnsResolver mockDnsResolver = host =>
            {
                // Return IP directly if host is an IP
                if (IPAddress.TryParse(host, out _)) return Task.FromResult(new[] {IPAddress.Parse(host)});

                // Assuming host is a hostname, returning mock IP
                if (mockHostIpMap.TryGetValue(host, out var ip))
                {
                    return Task.FromResult(new[] {ip});
                }

                // No matching host -> IP, return empty array
                return Task.FromResult(Array.Empty<IPAddress>());
            };
            _qosRunner = new BaselibQosRunner(mockQosJobProvider, mockDnsResolver);
        }

        [UnityTest]
        public IEnumerator Test_MeasureQosAsync_SortsResults()
        {
            // given
            // mockServers defined in class

            yield return AsyncTestHelpers.ExecuteTask(async() =>
            {
                // when
                var results = await _qosRunner.MeasureQosAsync(mockServers);

                // then
                var expectedResults = new List<Internal.QosResult>()
                {
                    new Internal.QosResult {Region = "region1", AverageLatencyMs = 53, PacketLossPercent = 0},
                    new Internal.QosResult {Region = "region2", AverageLatencyMs = 94, PacketLossPercent = 0.1f},
                    new Internal.QosResult {Region = "region3", AverageLatencyMs = 287, PacketLossPercent = 0.4f}
                };
                CollectionAssert.AreEqual(expectedResults, results, new QosComparer());
            });
        }

        [UnityTest]
        public IEnumerator Test_MeasureQosAsyncV2_OK()
        {
            // given
            var mockServersV2 = new List<V2.Models.QosServer>
            {
                new V2.Models.QosServer(new List<string> {"127.0.0.1:666"}, new QosServerAnnotations()),
                new V2.Models.QosServer(new List<string> {"127.0.0.1:777"}, new QosServerAnnotations()),
                new V2.Models.QosServer(new List<string> {"127.0.0.1:888"}, new QosServerAnnotations()),
            };
            // the _mockQosJob is setup to return 3 results.

            yield return AsyncTestHelpers.ExecuteTask(async() =>
            {
                // when
                var results = await _qosRunner.MeasureQosV2Async(mockServersV2);

                // then
                var expectedResults = new List<(V2.Models.QosServer, IQosMeasurements)>()
                {
                    (mockServersV2[0], new BaselibQosRunner.QosMeasurementImpl(averageLatencyMs: 53,  packetLossPercent: 0)),
                    (mockServersV2[1], new BaselibQosRunner.QosMeasurementImpl(averageLatencyMs: 94,  packetLossPercent: 0.1f)),
                    (mockServersV2[2], new BaselibQosRunner.QosMeasurementImpl(averageLatencyMs: 287,  packetLossPercent: 0.4f)),
                };
                CollectionAssert.AreEqual(expectedResults, results, new QosComparer());
            });
        }

        [UnityTest]
        public IEnumerator Test_MeasureQosAsyncV2_WhenServerCountMismatch_ReturnsEmpty()
        {
            // given
            var mockServersV2 = new List<V2.Models.QosServer>
            {
                new V2.Models.QosServer(new List<string> {"127.0.0.1:666"}, new QosServerAnnotations()),
                new V2.Models.QosServer(new List<string> {"127.0.0.1:777"}, new QosServerAnnotations()),
                new V2.Models.QosServer(new List<string> {"127.0.0.1:888"}, new QosServerAnnotations()),
                new V2.Models.QosServer(new List<string> {"127.0.0.1:999"}, new QosServerAnnotations()),
            };
            // the mockQosJob is setup to return only 3 results.

            yield return AsyncTestHelpers.ExecuteTask(async() =>
            {
                // when
                var results = await _qosRunner.MeasureQosV2Async(mockServersV2);

                // then
                CollectionAssert.IsEmpty(results);
            });
        }

        [UnityTest]
        public IEnumerator Test_MeasureServiceQosAsync_SortsResults()
        {
            // given
            // mockServers defined in class

            yield return AsyncTestHelpers.ExecuteTask(async() =>
            {
                // when
                var results = await _qosRunner.MeasureQosAsync(mockServiceServers);

                // then
                var expectedResults = new List<QosAnnotatedResult>()
                {
                    new QosAnnotatedResult {Region = "region1", AverageLatencyMs = 53, PacketLossPercent = 0, Annotations = mockAnnotation},
                    new QosAnnotatedResult {Region = "region2", AverageLatencyMs = 94, PacketLossPercent = 0.1f, Annotations = mockAnnotation},
                    new QosAnnotatedResult {Region = "region3", AverageLatencyMs = 287, PacketLossPercent = 0.4f, Annotations = mockAnnotation}
                };
                CollectionAssert.AreEqual(expectedResults, results, new QosComparer());
            });
        }

        [UnityTest]
        public IEnumerator Test_MeasureQosAsync_NoReceivedResultsDoesntOverflow()
        {
            // given
            var mockResults = new[]
            {
                new InternalQosResult {RequestsSent = 10, ResponsesReceived = 10},
                new InternalQosResult {RequestsSent = 10, ResponsesReceived = 9},
                new InternalQosResult {RequestsSent = 10, ResponsesReceived = 0}
            };
            mockResults[0].AddAggregateLatency(53 * 10);
            mockResults[1].AddAggregateLatency(94 * 9);
            // dispose mock results created in Setup() since we won't be using them
            _nativeMockResults.Dispose();
            _nativeMockResults = new NativeArray<InternalQosResult>(mockResults, Allocator.Persistent);
            _mockQosJob.SetupGet(x => x.QosResults).Returns(_nativeMockResults);

            yield return AsyncTestHelpers.ExecuteTask(async() =>
            {
                // when
                var results = await _qosRunner.MeasureQosAsync(mockServers);

                // then
                var expectedResults = new List<Internal.QosResult>()
                {
                    new Internal.QosResult {Region = "region1", AverageLatencyMs = 53, PacketLossPercent = 0},
                    new Internal.QosResult {Region = "region2", AverageLatencyMs = 94, PacketLossPercent = 0.1f},
                    new Internal.QosResult {Region = "region3", AverageLatencyMs = int.MaxValue, PacketLossPercent = 1f}
                };
                CollectionAssert.AreEqual(expectedResults, results, new QosComparer());
            });
        }

        [UnityTest]
        public IEnumerator Test_MeasureQosAsync_AcceptsValidIpV4Servers()
        {
            // given
            // mockServers defined in class

            // when
            yield return AsyncTestHelpers.ExecuteTask(_qosRunner.MeasureQosAsync(mockServers));

            // then
            var expectedServers = new List<UcgQosServer>
            {
                new UcgQosServer {regionid = "region1", ipv4 = "127.0.0.1", port = 666},
                new UcgQosServer {regionid = "region2", ipv4 = "127.0.0.1", port = 777},
                new UcgQosServer {regionid = "region3", ipv4 = "127.0.0.1", port = 888}
            };
            CollectionAssert.AreEqual(expectedServers, _calledServers);
        }

        [UnityTest]
        public IEnumerator Test_MeasureQosAsync_AcceptsValidIpV6Servers()
        {
            // given
            var mockServersWithIpv6 = new List<QosServer>
            {
                new QosServer(new List<string> {"[0000:0000:0000:0000:0000:FFFF:0A00:0001]:6666"}, "region1",
                    new List<string> {"relay"}),
                new QosServer(new List<string> {"[0000:0000:0000:0000:0000:FFFF:0A00:0002]:7777"}, "region2",
                    new List<string> {"relay"}),
            };

            // when
            yield return AsyncTestHelpers.ExecuteTask(_qosRunner.MeasureQosAsync(mockServersWithIpv6));

            // then
            var expectedServers = new List<UcgQosServer>
            {
                new UcgQosServer {regionid = "region1", ipv6 = "::ffff:10.0.0.1", port = 6666},
                new UcgQosServer {regionid = "region2", ipv6 = "::ffff:10.0.0.2", port = 7777},
            };
            CollectionAssert.AreEqual(expectedServers, _calledServers);
        }

        [UnityTest]
        public IEnumerator Test_MeasureQosAsync_AcceptsValidHostNameServers()
        {
            // given
            var mockServersWithHostnames = new List<QosServer>
            {
                new QosServer(new List<string> {"fancy-qos-hostname-us-east1.example.com:6666"}, "region1",
                    new List<string> {"relay"}),
                new QosServer(new List<string> {"its-ya-boi.example.com:7777"}, "region2", new List<string> {"relay"}),
            };

            // when
            yield return AsyncTestHelpers.ExecuteTask(_qosRunner.MeasureQosAsync(mockServersWithHostnames));

            // then
            var expectedServers = new List<UcgQosServer>
            {
                new UcgQosServer {regionid = "region1", ipv4 = "10.0.0.1", port = 6666},
                new UcgQosServer {regionid = "region2", ipv4 = "10.0.0.2", port = 7777},
            };
            CollectionAssert.AreEqual(expectedServers, _calledServers);
        }

        [UnityTest]
        public IEnumerator Test_MeasureQosAsync_HandlesNoDnsResolution()
        {
            // given
            var mockServersWithHostnames = new List<QosServer>
            {
                new QosServer(new List<string> {"invalid-dns-name.example.com:6666"}, "region1",
                    new List<string> {"relay"}),
            };
            LogAssert.Expect(LogType.Error,
                "No addresses could be resolved for host invalid-dns-name.example.com.");

            // when
            yield return AsyncTestHelpers.ExecuteTask(_qosRunner.MeasureQosAsync(mockServersWithHostnames));

            // then
            CollectionAssert.IsEmpty(_calledServers);
        }

        [UnityTest]
        public IEnumerator Test_MeasureQosAsync_HandlesDnsResolutionException()
        {
            // given
            DnsResolver exceptionThrowingDnsResolver =
                _ => Task.FromException<IPAddress[]>(new Exception("DNS resolution exception"));
            _qosRunner = new BaselibQosRunner((servers, title) => _mockQosJob.Object, exceptionThrowingDnsResolver);

            var mockServersWithHostnames = new List<QosServer>
            {
                new QosServer(new List<string> {"invalid-dns-name.example.com:6666"}, "region1",
                    new List<string> {"relay"}),
            };

            async Task TestAsync()
            {
                // when/then
                await AsyncTestHelpers.ThrowsAsync<Exception>(
                    () => _qosRunner.MeasureQosAsync(mockServersWithHostnames));

                // dispose mock results since they would never get created in the first place due to the thrown exception
                _nativeMockResults.Dispose();
            }

            yield return AsyncTestHelpers.ExecuteTask(TestAsync());
        }

        [UnityTest]
        public IEnumerator Test_MeasureQosAsync_FiltersOutInvalidEndpoints()
        {
            // given
            IList<UcgQosServer> calledServers = null;
            _qosRunner = new BaselibQosRunner((servers, _) =>
            {
                calledServers = servers;
                return _mockQosJob.Object;
            });
            var mockServersWithBadEndpoint = new List<QosServer>
            {
                // valid endpoints
                new QosServer(new List<string> {"127.0.0.1:666"}, "region1", new List<string> {"relay"}),
                new QosServer(new List<string> {"127.0.0.1:777"}, "region2", new List<string> {"relay"}),
                // bad address
                new QosServer(new List<string> {"bad endpoint"}, "region3", new List<string> {"relay"}),
                // missing port
                new QosServer(new List<string> {"126"}, "region4", new List<string> {"relay"}),
            };
            LogAssert.Expect(LogType.Error, "Could not create address from endpoint: 'bad endpoint'.");
            LogAssert.Expect(LogType.Error, "Missing or invalid port in endpoint: '126'.");

            // when
            yield return AsyncTestHelpers.ExecuteTask(_qosRunner.MeasureQosAsync(mockServersWithBadEndpoint));

            // then
            var expectedServers = new List<UcgQosServer>
            {
                new UcgQosServer() {regionid = "region1", ipv4 = "127.0.0.1", port = 666},
                new UcgQosServer() {regionid = "region2", ipv4 = "127.0.0.1", port = 777}
            };
            CollectionAssert.AreEqual(expectedServers, calledServers);
        }

        [UnityTest]
        public IEnumerator Test_QosRequestSerializeAndroidLittleEndian()
        {
            // The QoS servers expect the data to be transmitted in the little endian format. This test verifies that Serialize does that.
            // See how the leftmost (little) bytes of the Timestamp (0x78 and 0x56) are sent first on the wire.
            var rq = new QosRequest
            {
                Title = new byte[] {0xAA, 0x12}, Sequence = 12, Identifier = 0xD2C4, Timestamp = 0xABCD1234ABCD5678,
            };
            var expected = new byte[]
            {
                89, 0, 3, 0xAA, 0x12, 12, 0xC4, 0xD2, 0x78, 0x56, 0xCD, 0xAB, 0x34, 0x12, 0xCD, 0xAB
            };

            var buf = rq.Serialize();
            var got = buf.ToBytesNBC();

            CollectionAssert.AreEqual(expected, got);

            buf.Dispose();
            // dispose mock results created in Setup() since we won't be using them
            _nativeMockResults.Dispose();
            yield return null;
        }

#endif
    }
}
