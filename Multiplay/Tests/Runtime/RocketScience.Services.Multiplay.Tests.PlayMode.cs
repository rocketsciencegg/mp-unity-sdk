using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using RocketScience.Services.Multiplay.Apis.GameServer;
using RocketScience.Services.Multiplay.Internal;
using RocketScience.Services.Multiplay.Tests;
using RocketScience.Services.Multiplay.GameServer;
using Unity.Services.Core;
using System;
using Unity.Collections;
using UnityEngine;
using RocketScience.Services.Multiplay.Apis.Payload;
using RocketScience.Services.Multiplay.Models;
using RocketScience.Services.Multiplay.Http;
using Unity.Services.Wire.Internal;
using Unity.Services.Authentication.Internal;
using System.Text;
using System.Collections.Generic;
#if NUGET_MOQ_AVAILABLE && UNITY_EDITOR
using Moq;
#endif

namespace RocketScience.Services.Multiplay.Tests.PlayMode
{
    [TestFixture]
    public class MultiplayTestsPlayMode
    {
        private readonly ServerConfig testServerConfig = new ServerConfig(12345, "9F3D5DA6-ED87-49E2-9879-D658A57EF9BE", 12121, 7878, "0.0.0.0", "logs");

        bool m_OneTimeSetupComplete = false;

#if NUGET_MOQ_AVAILABLE && UNITY_EDITOR

        public class TestPayload
        {
            public bool somethingBooly;
            public int happiness;
        }

        private HttpClientResponse httpSuccessResponse;

        private Mock<IAccessToken> mockAccessToken;
        private Mock<IHttpClient> mockHttpClient;
        private Mock<IMultiplayServiceSdk> mockMultiplayServiceSdk;
        private Mock<IPayloadApiClient> mockPayloadApiClient;
        private Mock<IServerConfigReader> mockServerConfigReader;
        private Mock<IWireDirect> mockWire;
        private Mock<IChannel> mockWireChannel;
        private IMultiplayService multiplayService;
        private Configuration testConfiguration;
        private IGameServerApiClient testGameServerApiClient;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            m_OneTimeSetupComplete = true;
        }

        [UnitySetUp]
        public IEnumerator Setup()
        {
            yield return new WaitUntil(() => m_OneTimeSetupComplete);

            httpSuccessResponse = new HttpClientResponse(null, 200, false, false, null, null);
            testConfiguration = new Configuration("http://example.com/", 10, 3, null);
            mockHttpClient = new Mock<IHttpClient>();
            mockAccessToken = new Mock<IAccessToken>();
            mockMultiplayServiceSdk = new Mock<IMultiplayServiceSdk>();
            mockPayloadApiClient = new Mock<IPayloadApiClient>();
            mockServerConfigReader = new Mock<IServerConfigReader>();
            mockWire = new Mock<IWireDirect>();
            mockWireChannel = new Mock<IChannel>();
            testGameServerApiClient = new GameServerApiClient(mockHttpClient.Object, mockAccessToken.Object);

            mockMultiplayServiceSdk.Setup(_ => _.Configuration).Returns(testConfiguration);
            mockMultiplayServiceSdk.Setup(_ => _.GameServerApi).Returns(testGameServerApiClient);
            mockMultiplayServiceSdk.Setup(_ => _.PayloadApi).Returns(mockPayloadApiClient.Object);
            mockMultiplayServiceSdk.Setup(_ => _.ServerConfigReader).Returns(mockServerConfigReader.Object);
            mockServerConfigReader.Setup(_ => _.LoadServerConfig()).Returns(testServerConfig);
            mockMultiplayServiceSdk.Setup(_ => _.WireDirect).Returns(mockWire.Object);

            multiplayService = new WrappedMultiplayService(mockMultiplayServiceSdk.Object);
        }

        [UnityTest]
        public IEnumerator CanReportServerReady()
        {
            async Task TestAsync()
            {
                mockHttpClient.Setup(_ => _.MakeRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<int>()))
                    .Returns(Task.FromResult(new HttpClientResponse(null, 200, false, false, null, null)));

                await multiplayService.ReadyServerForPlayersAsync();
            }

            yield return AsyncTestHelpers.ExecuteTask(TestAsync());
        }

        [UnityTest]
        [Ignore("[MPSSDK-456] This is failing in our CI pipeline but not locally. Ignoring until we can fix it.")]
        public IEnumerator CanReceieveServerEvents()
        {
            bool stateChanged = false;
            MultiplayServerSubscriptionState subscriptionState = MultiplayServerSubscriptionState.Unsubscribed;

            // Validates that immediately after subscribing to the events we start receiving updates.
            async Task TestAsync()
            {
                MultiplayEventCallbacks callbacks = new MultiplayEventCallbacks();
                callbacks.SubscriptionStateChanged += (state) => { stateChanged = true; subscriptionState = state; };
                // Aiming to replicate the immediate subscription and state change when calling SubscribeAsync().
                mockWire.Setup(_ => _.CreateChannel(It.IsAny<string>(), It.IsAny<IChannelTokenProvider>())).Returns(mockWireChannel.Object);
                mockWireChannel.Setup(_ => _.SubscribeAsync()).Callback(() => mockWireChannel.Raise(_ => _.NewStateReceived += null, SubscriptionState.Subscribing));
                await multiplayService.SubscribeToServerEventsAsync(callbacks);
            }

            yield return AsyncTestHelpers.ExecuteTask(TestAsync());

            Assert.IsTrue(stateChanged);
            Assert.IsTrue(subscriptionState == MultiplayServerSubscriptionState.Subscribing);
        }

#endif
    }
}
