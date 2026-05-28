using System;
using System.Net;
using System.Net.Sockets;
using Unity.Collections;
using UnityEngine;

namespace Unity.Ucg.Usqp.Tests
{
    class UsqpClient
    {
        public enum UsqpClientState
        {
            Idle,
            WaitingForChallenge,
            WaitingForResponse,
            Success,
            Failure
        }

        const int k_BufferSize = 1472;

        public bool enableVerboseLogging = false;
        public int timeOutMs = 5000;

        NativeArray<byte> m_Buffer = new NativeArray<byte>(k_BufferSize, Allocator.Persistent);
        byte[] m_RecvBuffer = new byte[k_BufferSize];
        uint m_ChallengeId;
        EndPoint m_Endpoint = new IPEndPoint(0, 0);
        readonly IPEndPoint m_Server;
        Socket m_Socket;
        DateTime m_Time;

        public UsqpClient(IPEndPoint server)
        {
            m_Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            SetupAndBind(m_Socket, IPAddress.Any, 0);

            m_Server = server;

            ClientState = new UsqpClientState();
            m_Time = DateTime.UtcNow;
        }

        public void Dispose()
        {
            m_Buffer.Dispose();
        }

        static SocketError SetupAndBind(Socket socket, IPAddress addressToBind, int portToBind)
        {
            var error = SocketError.Success;
            socket.Blocking = false;

            var ep = new IPEndPoint(addressToBind ?? IPAddress.Any, portToBind);

            try
            {
                socket.Bind(ep);
            }
            catch (SocketException e)
            {
                error = e.SocketErrorCode;
                throw;
            }

            return error;
        }

        public ServerInfo ServerInfo { get; private set; } = new ServerInfo();
        public UsqpClientState ClientState { get; private set; }

        private void Send(ref DataStreamWriter writer, UsqpClientState newState)
        {
            m_Socket.SendTo(writer.AsNativeArray().ToArray(), writer.Length, SocketFlags.None, m_Server);
            ClientState = newState;
        }

        public void StartInfoQuery()
        {
            Debug.Assert(ClientState == UsqpClientState.Idle);
            m_Time = DateTime.UtcNow;

            var writer = new DataStreamWriter(m_Buffer);
            var req = new ChallengeRequest();
            req.ToStream(ref writer);

            Send(ref writer, UsqpClientState.WaitingForChallenge);
        }

        void SendServerInfoQuery()
        {
            m_Time = DateTime.UtcNow;

            var req = new QueryRequest
            {
                Header = { ChallengeId = m_ChallengeId },
                RequestedChunks = (byte)UsqpChunkType.ServerInfo
            };

            var writer = new DataStreamWriter(m_Buffer);
            req.ToStream(ref writer);

            Send(ref writer, UsqpClientState.WaitingForResponse);
        }

        public void Update()
        {
            // Early-exit if we're already done
            if (ClientState == UsqpClientState.Success || ClientState == UsqpClientState.Failure)
            {
                // Close socket if it's still open
                m_Socket?.Close(0);
                return;
            }

            if (m_Socket.Poll(0, SelectMode.SelectRead))
            {
                var read = 0;

                try
                {
                    read = m_Socket.ReceiveFrom(m_RecvBuffer, m_RecvBuffer.Length, SocketFlags.None, ref m_Endpoint);
                }
                catch (SocketException ex)
                {
                    Debug.LogWarning(ex.Message);
                }

                if (read > 0)
                {
                    // TODO(steve): Convert to use transport so we don't have to perform a copy.
                    NativeArray<byte>.Copy(m_RecvBuffer, m_Buffer, read);
                    var reader = new DataStreamReader(m_Buffer.GetSubArray(0, read));
                    var header = new UsqpHeader();
                    header.FromStream(ref reader);

                    switch (ClientState)
                    {
                        case UsqpClientState.Idle:
                            break;

                        case UsqpClientState.WaitingForChallenge:

                            if ((UsqpMessageType)header.Type == UsqpMessageType.ChallengeResponse && m_Endpoint.Equals(m_Server))
                            {
                                m_ChallengeId = header.ChallengeId;
                                SendServerInfoQuery();
                            }

                            break;

                        case UsqpClientState.WaitingForResponse:

                            if ((UsqpMessageType)header.Type == UsqpMessageType.QueryResponse)
                            {
                                var rsp = new ServerInfo();
                                rsp.FromStream(ref reader, false);

                                if (enableVerboseLogging)
                                    Debug.Log($"ServerName: {rsp.ServerInfoData.ServerName}" +
                                        $", BuildId: {rsp.ServerInfoData.BuildId}" +
                                        $", Current Players: {rsp.ServerInfoData.CurrentPlayers}" +
                                        $", Max Players: {rsp.ServerInfoData.MaxPlayers}" +
                                        $", GameType: {rsp.ServerInfoData.GameType}" +
                                        $", Map: {rsp.ServerInfoData.Map}" +
                                        $", Port: {rsp.ServerInfoData.Port}");

                                ServerInfo = rsp;
                                ClientState = UsqpClientState.Success;
                                m_Time = DateTime.UtcNow;
                            }

                            break;

                        case UsqpClientState.Success:
                        case UsqpClientState.Failure:
                        default:
                            break;
                    }
                }
            }

            var took = DateTime.UtcNow.Subtract(m_Time);
            if (took.TotalMilliseconds > timeOutMs)
            {
                Debug.Log($"SQP Client failed due to timeout in state {ClientState} ({took.TotalMilliseconds }ms > {timeOutMs}ms)");
                ClientState = UsqpClientState.Failure;
            }

            if (ClientState == UsqpClientState.Success || ClientState == UsqpClientState.Failure)
                m_Socket?.Close(0);
        }
    }
}
