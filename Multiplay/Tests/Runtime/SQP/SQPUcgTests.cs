using System;
using System.Collections.Generic;
using System.Net;
using NUnit.Framework;
using UnityEngine;

namespace Unity.Ucg.Usqp.Tests
{
    class SqpUcgTests
    {
        // ------------- SETUP -------------

        static ushort s_CurrentServerPort = 9000;

        [SetUp]
        public void Setup() {}

        [TearDown]
        public void Teardown() {}

        public static UsqpServer GetTestServer()
        {
            s_CurrentServerPort++;
            var server = new UsqpServer(s_CurrentServerPort);
            PopulateServerInfoData(server);
            return server;
        }

        public static UsqpServer GetTestServer(IPEndPoint endpoint)
        {
            var server = new UsqpServer(endpoint);
            PopulateServerInfoData(server);
            return server;
        }

        public static void PopulateServerInfoData(UsqpServer server)
        {
            server.ServerInfoData.ServerName = "Banana Boy Adventures";
            server.ServerInfoData.BuildId = "2018-1";
            server.ServerInfoData.CurrentPlayers = 1;
            server.ServerInfoData.MaxPlayers = 20;
            server.ServerInfoData.Port = ushort.Parse(server.ServerEndpoint.Port.ToString());
            server.ServerInfoData.GameType = "Capture the egg";
            server.ServerInfoData.Map = "Great escape to the sea";
        }

        public static UsqpClient GetTestClientForServer(UsqpServer server)
        {
            var endpoint = new IPEndPoint(server.ServerEndpoint.Address, server.ServerEndpoint.Port);
            if (endpoint.Address.Equals(IPAddress.Any))
                endpoint.Address = IPAddress.Loopback;
            var client = new UsqpClient(endpoint);

            return client;
        }

        public static void AssertIfServerInfoDataDoesNotMatch(ServerInfo.Data server1, ServerInfo.Data server2)
        {
            Assert.AreEqual(server1.BuildId, server2.BuildId);
            Assert.AreEqual(server1.CurrentPlayers, server2.CurrentPlayers);
            Assert.AreEqual(server1.GameType, server2.GameType);
            Assert.AreEqual(server1.Map, server2.Map);
            Assert.AreEqual(server1.MaxPlayers, server2.MaxPlayers);
            Assert.AreEqual(server1.Port, server2.Port);
            Assert.AreEqual(server1.ServerName, server2.ServerName);
        }
    }

    class StressTests
    {
        static void SQP_ServerHandlesAllPendingRequestsInOneFrame(int numberOfClients)
        {
            var server = SqpUcgTests.GetTestServer();
            server.Update();

            // Wait a bit for the server
            System.Threading.Thread.Sleep(10);

            // Create a bunch of clients and start queries
            var clients = new List<UsqpClient>();
            for (var i = 0; i < numberOfClients; i++)
            {
                var client = SqpUcgTests.GetTestClientForServer(server);
                clients.Add(client);
                client.StartInfoQuery();
            }

            // Wait a bit to make sure all packets are ready for consumption
            System.Threading.Thread.Sleep(100);

            // Update server + clients until all packets have been exchanged or we run out of iterations
            var serverUpdates = 0;
            var clientUpdates = 0;
            var iterations = 0;
            var allQueriesRepliedTo = false;

            /* --- State Progression ---
             * Server: [startup]
             * Client: send challenge (client.StartInfoQuery())
             * Server: respond to challenge (server.Update() #1)
             * Client: send query (client.Update() #1)
             * Server: respond to query (server.Update() #2)
             * Client: process query reply and set state to success (client.Update() #2)
             */

            // Run through the server + client state machines
            while (!allQueriesRepliedTo && iterations < 100)
            {
                // Update Server
                server.Update();
                serverUpdates++;

                allQueriesRepliedTo = true;

                // Update Client
                foreach (var client in clients)
                    if (client.ClientState != UsqpClient.UsqpClientState.Success)
                    {
                        client.Update();
                        clientUpdates++;

                        if (client.ClientState != UsqpClient.UsqpClientState.Success)
                            allQueriesRepliedTo = false;
                    }

                iterations++;
            }

            Debug.Log($"number of server Updates: {serverUpdates}");
            Debug.Log($"number of client Updates: {clientUpdates}");

            try
            {
                if (iterations > 100)
                {
                    Debug.Log("Iteration max reached without success, client states:");

                    foreach (var client in clients)
                        Debug.Log(client.ClientState.ToString());
                }
                else
                {
                    // Validate data
                    foreach (var client in clients)
                    {
                        SqpUcgTests.AssertIfServerInfoDataDoesNotMatch(server.ServerInfoData, client.ServerInfo.ServerInfoData);
                    }
                }
            }
            finally
            {
                foreach (var client in clients)
                {
                    client.Dispose();
                }
                server.Dispose();
            }

            Assert.Less(iterations, 100); // If everything is working properly we should only need 2 iterations

            // TODO - This is flaky on non-windows platforms, possibly due to how we use sockets
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            Assert.LessOrEqual(serverUpdates, 2); // If everything is working properly we should only need 2 updates
            Assert.LessOrEqual(clientUpdates, numberOfClients * 2); // If everything is working properly we should only need 2 updates (per client)
#endif
        }

        [Test]
        public void SQP_ServerHandlesAllPendingRequestsInOneFrame_1_Client()
        {
            SQP_ServerHandlesAllPendingRequestsInOneFrame(1);
        }

        [Test]
        public void SQP_ServerHandlesAllPendingRequestsInOneFrame_10_Clients()
        {
            SQP_ServerHandlesAllPendingRequestsInOneFrame(10);
        }

#if PLATFORM_STANDALONE_WIN

        // It seems like this may be somewhat fickle on non-Windows devices
        [Test]
        public void SQP_ServerHandlesAllPendingRequestsInOneFrame_100_Clients()
        {
            SQP_ServerHandlesAllPendingRequestsInOneFrame(100);
        }

        // Currently only working on windows; appears to open too many sockets/streams simultaneously on Mac
        // It seems like this may be somewhat fickle; I've seen it fail sometimes because it takes a couple more updates than expected (i.e. 8 vs 2)
        [Test]
        public void SQP_ServerHandlesAllPendingRequestsInOneFrame_1000_Clients()
        {
            SQP_ServerHandlesAllPendingRequestsInOneFrame(1000);
        }

#endif
    }
}
