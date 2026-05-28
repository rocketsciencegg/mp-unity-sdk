#if UGS_MATCHMAKER_AVAILABLE && NUGET_MOQ_AVAILABLE && UNITY_EDITOR
using Moq;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.TestTools;
using RocketScience.Multiplay.Internal;
using UnityEngine;
using RocketScience.Multiplay.Apis.Payload;
using RocketScience.Multiplay.Http;
using Unity.Services.Matchmaker.Models;
using Unity.Services.Authentication.Internal;

namespace RocketScience.Multiplay.Tests.PlayMode
{
    [TestFixture]
    public class MultiplayTestsPlayMode
    {
        private readonly ServerConfig testServerConfig = new ServerConfig(12345, "9F3D5DA6-ED87-49E2-9879-D658A57EF9BE", 12121, 7878, "0.0.0.0", "logs");
        MatchmakingResults expectedResult = new MatchmakingResults
            (
            new MatchProperties
            (
                new List<Team> { new Team("Red Team", "441b221b-ee46-48f5-8911-201c79a04d49", new List<string> { "jVR5X2Pn2lxzZP7uZ0onVqFsubFc" }) },
                new List<Player> { new Player("jVR5X2Pn2lxzZP7uZ0onVqFsubFc", new { skill = 455.6 }) },
                "687b64be-36bf-4f5f-a5b3-355a73090b80",
                "27d22bd3-b0ce-4d43-ac7f-db09a5e958c7"
            ),
            "default-pool",
            "default-queue",
            "Default pool",
            "fb3e37e3-e28f-40e4-808b-2f9bcd5a8705",
            "27d22bd3-b0ce-4d43-ac7f-db09a5e958c7",
            "bb1a2a4c-8426-4075-8865-0219642976e1",
            "2bd990e8-1bad-4284-9d04-d0d44490accb"
            );

        string literalJson =
            @"{""MatchProperties"":{""Teams"":[{""TeamName"":""Red Team"",""TeamID"":""441b221b-ee46-48f5-8911-201c79a04d49"",""PlayerIDs"":[""jVR5X2Pn2lxzZP7uZ0onVqFsubFc""]}],""Players"":[{""Id"":""jVR5X2Pn2lxzZP7uZ0onVqFsubFc"",""CustomData"":{""skill"":455.6}}],""Region"":""687b64be-36bf-4f5f-a5b3-355a73090b80"",""BackfillTicketId"":""27d22bd3-b0ce-4d43-ac7f-db09a5e958c7""},""GeneratorName"":""default-pool"",""QueueName"":""default-queue"",""PoolName"":""Default pool"",""EnvironmentId"":""fb3e37e3-e28f-40e4-808b-2f9bcd5a8705"",""BackfillTicketId"":""27d22bd3-b0ce-4d43-ac7f-db09a5e958c7"",""MatchId"":""bb1a2a4c-8426-4075-8865-0219642976e1"",""PoolId"":""2bd990e8-1bad-4284-9d04-d0d44490accb""}";


        bool m_OneTimeSetupComplete = false;

        private Mock<IMultiplayServiceSdk> mockMultiplayServiceSdk;
        private Mock<IPayloadApiClient> mockPayloadApiClient;
        private Mock<IServerConfigReader> mockServerConfigReader;
        private Mock<IAccessToken> mockAccessToken;
        private Mock<IHttpClient> mockHttpClient;
        private IMultiplayService multiplayService;
        private PayloadApiClient testPayloadApiClient;
        private Configuration testConfiguration;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            m_OneTimeSetupComplete = true;
        }

        [UnitySetUp]
        public IEnumerator Setup()
        {
            yield return new WaitUntil(() => m_OneTimeSetupComplete);

            testConfiguration = new Configuration("http://example.com/", 10, 3, null);
            mockHttpClient = new Mock<IHttpClient>();
            mockAccessToken = new Mock<IAccessToken>();
            mockPayloadApiClient = new Mock<IPayloadApiClient>();
            mockServerConfigReader = new Mock<IServerConfigReader>();
            mockMultiplayServiceSdk = new Mock<IMultiplayServiceSdk>();
            testPayloadApiClient = new PayloadApiClient(mockHttpClient.Object, mockAccessToken.Object);

            mockMultiplayServiceSdk.Setup(_ => _.Configuration).Returns(testConfiguration);
            mockMultiplayServiceSdk.Setup(_ => _.PayloadApi).Returns(mockPayloadApiClient.Object);
            mockMultiplayServiceSdk.Setup(_ => _.ServerConfigReader).Returns(mockServerConfigReader.Object);
            mockServerConfigReader.Setup(_ => _.LoadServerConfig()).Returns(testServerConfig);
            multiplayService = new WrappedMultiplayService(mockMultiplayServiceSdk.Object);

            mockHttpClient.Setup(_ => _.MakeRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<int>()))
                .Returns(Task.FromResult(new HttpClientResponse(null, 200, false, false, Encoding.UTF8.GetBytes(literalJson), null)));

            mockMultiplayServiceSdk.Setup(_ => _.PayloadApi).Returns(testPayloadApiClient);
        }

        [UnityTest]
        public IEnumerator CanDeserializeMatchmakingResultsAsText()
        {
            async Task TestAsync()
            {
                var payloadAsText = await multiplayService.GetPayloadAllocationAsPlainText();
                Assert.AreEqual(literalJson, payloadAsText);
            }

            yield return AsyncTestHelpers.ExecuteTask(TestAsync());
        }

        [UnityTest]
        public IEnumerator CanDeserializeMatchmakingResultsAsObject()
        {
            async Task TestAsync()
            {
                var payloadAsObject = await multiplayService.GetPayloadAllocationFromJsonAs<MatchmakingResults>();
                Assert.IsNotNull(payloadAsObject);

                //There is no equality operator provided by generated code, so memberwise checking.
                Assert.AreEqual(expectedResult.BackfillTicketId,  payloadAsObject.BackfillTicketId);
                Assert.AreEqual(expectedResult.QueueName, payloadAsObject.QueueName);
                Assert.AreEqual(expectedResult.GeneratorName, payloadAsObject.GeneratorName);
                Assert.AreEqual(expectedResult.EnvironmentId, payloadAsObject.EnvironmentId);
                Assert.AreEqual(expectedResult.MatchId, payloadAsObject.MatchId);
                Assert.AreEqual(expectedResult.PoolId, payloadAsObject.PoolId);

                //Comparing MatchProperties
                Assert.AreEqual(expectedResult.MatchProperties.BackfillTicketId, payloadAsObject.MatchProperties.BackfillTicketId);
                Assert.AreEqual(expectedResult.MatchProperties.Region, payloadAsObject.MatchProperties.Region);
                Assert.AreEqual(expectedResult.MatchProperties.Players.Count, payloadAsObject.MatchProperties.Players.Count);
                Assert.AreEqual(expectedResult.MatchProperties.Teams.Count, payloadAsObject.MatchProperties.Teams.Count);
            }

            yield return AsyncTestHelpers.ExecuteTask(TestAsync());
        }
    }
}
#endif
