#if !UNITY_WEBGL

using System.Collections;
using Newtonsoft.Json;
using NUnit.Framework;
using RocketScience.Services.Wire.Internal;
using Unity.Services.Wire.Internal;
using UnityEngine;
using UnityEngine.TestTools;
using Logger = RocketScience.Services.Wire.Internal.Logger;

namespace RocketScience.Services.WireDirect.Tests
{
    class ConcurrencyTests : BaseClient
    {
        [SetUp]
        public void Setup() {}

        [TearDown]
        public void Teardown() {}

        [UnityTest]
        public IEnumerator BatchSubscribeAndMessage()
        {
            var ct = client.ConnectAsync();
            websocketMock.InvokeOnOpen();
            yield return Utils.TaskToIEnumerator(websocketMock.EmulateReceive(new Reply(1, null, new Result()).ToJson()));
            yield return new WaitForDone(2f, () => ct.IsCompleted);
            Assert.IsTrue(ct.IsCompleted, "connection task should be complete");
            Assert.IsFalse(ct.IsFaulted, "connection task should not be faulted");
            var tp = new TokenProvider();
            var channel = client.CreateChannel(tp);
            bool errReceived = false;
            string message = string.Empty;

            channel.ErrorReceived += _ => errReceived = true;
            channel.MessageReceived += m => message = m;
            var st = channel.SubscribeAsync();
            var subReply = JsonConvert.SerializeObject(new Reply(CommandID.currentId, null, new Result()));
            var publication = $"{{\"result\":{{\"channel\":\"{tp.data.ChannelName}\",\"data\":{{\"data\":{{\"message\":\"test 0\"}},\"offset\":248}}}}}}";
            var batch = subReply + "\n" + publication;
            Debug.Log("batch:");
            Debug.Log(batch);
            yield return Utils.TaskToIEnumerator(websocketMock.EmulateReceive(
                System.Text.Encoding.UTF8.GetBytes(batch)));

            yield return new WaitForDone(0.5f, () => !string.IsNullOrEmpty(message));

            Assert.IsTrue(st.IsCompleted, "subscription task should be complete");
            Assert.IsFalse(st.IsFaulted, "subscription task should not be faulted");
            Assert.IsFalse(errReceived, "should not have received an error");
            Assert.IsFalse(string.IsNullOrEmpty(message), "message shouldn't be empty");
            var originalPayload = JsonConvert.DeserializeObject<MsgClass>("{\"message\":\"test 0\"}");
            var receivedPayload = JsonConvert.DeserializeObject<MsgClass>(message);
            Assert.IsTrue(string.Equals(originalPayload.message, receivedPayload.message), $"message isn't the one expected. Got: [{receivedPayload.message}]");
        }
    }

    class MsgClass
    {
        public string message;
    }
}

#endif
