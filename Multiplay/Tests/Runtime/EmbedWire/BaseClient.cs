#if !UNITY_WEBGL

using System;
using System.Threading;

using NUnit.Framework;
using Unity.Services.Wire.Internal;
using RocketScience.Services.WireDirect.Tests.Stubs;
using RocketScience.Services.WireDirect.Tests.UnityThreadUtils;
using UnityEngine;
using RocketScience.Services.Wire.Internal;

namespace RocketScience.Services.WireDirect.Tests
{
    [TestFixture]
    internal abstract class BaseClient
    {
        internal WebsocketMock websocketMock;
        internal Client client;
        internal Configuration config;
        internal ActionScheduler m_ActionScheduler;


        [SetUp]
        public void InitializeClient()
        {
            m_ActionScheduler = new ActionScheduler();
            websocketMock = new WebsocketMock(m_ActionScheduler);
            websocketMock.mainThread = Thread.CurrentThread.ManagedThreadId;
            config = new Configuration
            {
                address = "mocked websocket",
                token = new AccessTokenStub(),
                WebSocket = websocketMock,
            };
            client = new Client(config, m_ActionScheduler, new MetricsMock(), new UnityThreadUtilsWrapper());
            client.SetupDirectClient();
        }

        [TearDown]
        public void TearDown()
        {
            websocketMock.AssertReplyQueueEmpty();
        }
    }
}
#endif
