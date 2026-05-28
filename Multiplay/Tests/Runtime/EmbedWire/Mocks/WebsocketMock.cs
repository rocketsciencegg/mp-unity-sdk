using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Services.Core.Scheduler.Internal;
using RocketScience.Services.Wire.Internal;

namespace RocketScience.Services.WireDirect.Tests.Stubs
{
    internal class WebsocketMock : TestStub, IWebSocket
    {
        public event WebSocketOpenEventHandler OnOpen;

        public event WebSocketMessageEventHandler OnMessage;

        public event WebSocketErrorEventHandler OnError;

        public event WebSocketCloseEventHandler OnClose;

        private string nextReply;

        private Queue<Reply> replyQueue = new Queue<Reply>();

        Queue<CentrifugeCloseCode> m_FailureCloseCodeQueue = new Queue<CentrifugeCloseCode>();

        private IActionScheduler m_ActionScheduler;

        private const double k_EmulatedLatencyInSeconds = 0.05;

        internal WebSocketState state = WebSocketState.Closed;

        internal int ReplyQueueCount => replyQueue.Count;

        public WebsocketMock(IActionScheduler actionScheduler)
        {
            m_ActionScheduler = actionScheduler;
        }

        void IWebSocket.Close(WebSocketCloseCode code, string reason)
        {
            if (state == WebSocketState.Closed)
            {
                return;
            }
            AddCall("Close", code, reason);
            if (state != WebSocketState.Open)
            {
                state = WebSocketState.Closing;
            }
        }

        public void InvokeOnClose(WebSocketCloseCode code)
        {
            state = WebSocketState.Closed;
            OnClose?.Invoke(code);
        }

        public void InvokeOnOpen()
        {
            state = WebSocketState.Open;
            OnOpen?.Invoke();
        }

        void IWebSocket.Connect()
        {
            if (state != WebSocketState.Closed)
            {
                throw new WebSocketInvalidStateException();
            }

            state = WebSocketState.Connecting;
            AddCall("Connect");
        }

        public void AcceptConnection()
        {
            Assert.AreEqual(state, WebSocketState.Connecting, "We shouldn't accept a connection when not in accepting state.");
            Logger.LogVerbose(
                $"[WebsocketMock] Emulating socket open. Listeners count: {OnOpen.GetInvocationList().Length}");
            state = WebSocketState.Open;
            OnOpen?.Invoke();
        }

        WebSocketState IWebSocket.GetState()
        {
            AddCall("GetState");
            return state;
        }

        public void SetNextMessage(CentrifugeError err)
        {
            replyQueue.Enqueue(new Reply(0, err, null));
        }

        public void SetNextMessage(Result result)
        {
            replyQueue.Enqueue(new Reply(0, null, result));
        }

        public void AssertReplyQueueEmpty()
        {
            Assert.IsEmpty(replyQueue);
        }

        public void SetNextCloseOnError(CentrifugeCloseCode code)
        {
            m_FailureCloseCodeQueue.Enqueue(code);
        }

        void IWebSocket.Send(byte[] data)
        {
            if (state != WebSocketState.Open)
            {
                throw new WebSocketInvalidStateException();
            }

            Command<object> command = Command<object>.FromJSON(data);
            // Ping should be handled differently.
            if (command.method == Message.Method.PING)
            {
                m_ActionScheduler.ScheduleAction(() =>
                {
                    if (state != WebSocketState.Open)
                    {
                        Logger.Log("do not answer, we are no longer connected.");
                        return;
                    }

                    Reply reply = new Reply(command.id, null, null);
                    OnMessage?.Invoke(reply.ToJson());
                });
                return;
            }

            // we only add the call if it's not a PING
            AddCall("Send", data);
        }

        public async Task EmulateReceive(byte[] message)
        {
            if (state != WebSocketState.Open)
            {
                throw new WebSocketInvalidStateException();
            }

            var tcs = new TaskCompletionSource<bool>();
            m_ActionScheduler.ScheduleAction(() =>
            {
                try
                {
                    OnMessage?.Invoke(message);
                    tcs.SetResult(true);
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            });
            await tcs.Task;
        }

        public void SendError(string error)
        {
            OnError?.Invoke(error);
        }
    }
}
