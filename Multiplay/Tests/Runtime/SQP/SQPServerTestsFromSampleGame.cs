using System;
using NUnit.Framework;
using Unity.Collections;
using System.Net;

namespace Unity.Ucg.Usqp.Tests
{
    class SqpServerTestsFromSampleGame
    {
        const int k_BufferSize = 1472;
        NativeArray<byte> m_Buffer;

        DataStreamReader m_Reader;
        DataStreamWriter m_Writer;

        [SetUp]
        public void Setup()
        {
            m_Buffer = new NativeArray<byte>(k_BufferSize, Allocator.Persistent);
            m_Writer = new DataStreamWriter(m_Buffer);
        }

        [TearDown]
        public void Teardown()
        {
            m_Buffer.Dispose();
        }

        [Test]
        public void SQP_SerializeChallengeRequest_NoError()
        {
            var snd = new ChallengeRequest();
            snd.ToStream(ref m_Writer);

            m_Reader = new DataStreamReader(m_Buffer.GetSubArray(0, m_Writer.Length));
            var rcv = new ChallengeRequest();
            rcv.FromStream(ref m_Reader);

            Assert.AreEqual((byte)UsqpMessageType.ChallengeRequest, rcv.Header.Type);
            Assert.AreEqual(snd.Header.ChallengeId, rcv.Header.ChallengeId);
        }

        [Test]
        public void SQP_SerializeChallengeResponse_NoError()
        {
            var snd = new ChallengeResponse();

            snd.Header.ChallengeId = 1337;

            snd.ToStream(ref m_Writer);

            var rcv = new ChallengeResponse();
            m_Reader = new DataStreamReader(m_Buffer.GetSubArray(0, m_Writer.Length));
            rcv.FromStream(ref m_Reader);

            Assert.AreEqual((byte)UsqpMessageType.ChallengeResponse, rcv.Header.Type);
            Assert.AreEqual(snd.Header.ChallengeId, rcv.Header.ChallengeId);
        }

        [Test]
        public void SQP_SerializeQueryRequest_NoError()
        {
            var id = (uint)1337;
            var chunk = (byte)31;

            var snd = new QueryRequest();

            snd.Header.ChallengeId = id;
            snd.RequestedChunks = chunk;

            snd.ToStream(ref m_Writer);

            var rcv = new QueryRequest();
            m_Reader = new DataStreamReader(m_Buffer.GetSubArray(0, m_Writer.Length));
            rcv.FromStream(ref m_Reader);

            Assert.AreEqual((byte)UsqpMessageType.QueryRequest, rcv.Header.Type);
            Assert.AreEqual(id, rcv.Header.ChallengeId);
            Assert.AreEqual(chunk, rcv.RequestedChunks);
        }

        [Test]
        public void SQP_SerializeQueryResponseHeader_NoError()
        {
            var id = (uint)1337;
            var version = (ushort)12345;
            var packet = (byte)3;
            var last = (byte)9;

            var snd = new QueryResponseHeader();

            snd.Header.ChallengeId = id;
            snd.Version = version;
            snd.CurrentPacket = packet;
            snd.LastPacket = last;

            snd.ToStream(ref m_Writer);

            var rcv = new QueryResponseHeader();
            m_Reader = new DataStreamReader(m_Buffer.GetSubArray(0, m_Writer.Length));
            rcv.FromStream(ref m_Reader);

            Assert.AreEqual((byte)UsqpMessageType.QueryResponse, rcv.Header.Type);
            Assert.AreEqual(id, rcv.Header.ChallengeId);
            Assert.AreEqual(version, rcv.Version);
            Assert.AreEqual(packet, rcv.CurrentPacket);
            Assert.AreEqual(last, rcv.LastPacket);
        }

        [Test]
        public void SQP_SerializeServerInfo_NoError()
        {
            var header = new QueryResponseHeader();

            header.Header.ChallengeId = 1337;
            header.Version = 12345;
            header.CurrentPacket = 12;
            header.LastPacket = 13;

            var snd = new ServerInfo();
            snd.QueryHeader = header;
            snd.ServerInfoData.CurrentPlayers = 34;
            snd.ServerInfoData.MaxPlayers = 35;
            snd.ServerInfoData.ServerName = "Server";
            snd.ServerInfoData.GameType = "GameType";
            snd.ServerInfoData.BuildId = "2018.3";
            snd.ServerInfoData.Map = "Level0";
            snd.ServerInfoData.Port = 35001;

            snd.ToStream(ref m_Writer);

            var rcv = new ServerInfo();
            m_Reader = new DataStreamReader(m_Buffer.GetSubArray(0, m_Writer.Length));
            rcv.FromStream(ref m_Reader);

            Assert.AreEqual((byte)UsqpMessageType.QueryResponse, rcv.QueryHeader.Header.Type);
            Assert.AreEqual(header.Header.ChallengeId, rcv.QueryHeader.Header.ChallengeId);
            Assert.AreEqual(header.Version, rcv.QueryHeader.Version);
            Assert.AreEqual(header.CurrentPacket, rcv.QueryHeader.CurrentPacket);
            Assert.AreEqual(header.LastPacket, rcv.QueryHeader.LastPacket);

            Assert.AreEqual(snd.ServerInfoData.CurrentPlayers, rcv.ServerInfoData.CurrentPlayers);
            Assert.AreEqual(snd.ServerInfoData.MaxPlayers, rcv.ServerInfoData.MaxPlayers);
            Assert.AreEqual(snd.ServerInfoData.ServerName, rcv.ServerInfoData.ServerName);
            Assert.AreEqual(snd.ServerInfoData.GameType, rcv.ServerInfoData.GameType);
            Assert.AreEqual(snd.ServerInfoData.BuildId, rcv.ServerInfoData.BuildId);
            Assert.AreEqual(snd.ServerInfoData.Port, rcv.ServerInfoData.Port);
        }

        [Test]
        public void SQPClientServer_ServerInfoQuery_ServerInfoReceived()
        {
            ushort port = 13337;
            var server = new UsqpServer(port);
            var endpoint = new IPEndPoint(IPAddress.Loopback, port);
            var client = new UsqpClient(endpoint);
            client.enableVerboseLogging = true;

            try
            {
                var sid = server.ServerInfoData;
                sid.ServerName = "Banana Boy Adventures";
                sid.BuildId = "2018-1";
                sid.CurrentPlayers = 1;
                sid.MaxPlayers = 20;
                sid.Port = 1337;
                sid.GameType = "Capture the egg.";
                sid.Map = "Great escape to the see";

                server.ServerInfoData = sid;

                client.StartInfoQuery();

                var iterations = 0;

                while (client.ClientState != UsqpClient.UsqpClientState.Success && iterations++ < 1000)
                {
                    server.Update();
                    client.Update();
                }

                Assert.Less(iterations, 1000);

                Assert.AreEqual(client.ClientState, UsqpClient.UsqpClientState.Success);

                var serverInfoDataReceived = client.ServerInfo.ServerInfoData;

                Assert.AreEqual(serverInfoDataReceived.BuildId, sid.BuildId);
                Assert.AreEqual(serverInfoDataReceived.CurrentPlayers, sid.CurrentPlayers);
                Assert.AreEqual(serverInfoDataReceived.GameType, sid.GameType);
                Assert.AreEqual(serverInfoDataReceived.Map, sid.Map);
                Assert.AreEqual(serverInfoDataReceived.MaxPlayers, sid.MaxPlayers);
                Assert.AreEqual(serverInfoDataReceived.Port, sid.Port);
                Assert.AreEqual(serverInfoDataReceived.ServerName, sid.ServerName);
            }
            finally
            {
                client.Dispose();
                server.Dispose();
            }
        }
    }
}
