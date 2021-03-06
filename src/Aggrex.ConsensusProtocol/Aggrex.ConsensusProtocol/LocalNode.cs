﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Aggrex.Common;
using Aggrex.Configuration;
using Aggrex.ConsensusProtocol.Messages;
using Aggrex.ConsensusProtocol.Messages.Transaction;
using Aggrex.ConsensusProtocol.Transaction;
using Aggrex.Network;
using Autofac;

namespace Aggrex.ConsensusProtocol
{
    /// <summary>
    /// Represents a client node in the system. 
    /// </summary>
    internal class LocalNode : ILocalNode
    {
        private readonly INetworkListenerLoop _networkListenerLoop;
        private readonly IUPnPPortForwarder _uPnPPortForwarder;
        private readonly IPeerTracker _peerTracker;
        private readonly ClientSettings _clientSettings;
        private readonly IPEndPoint _seedNodeEndPoint;
        private RemoteNode.Factory _remoteNodeFactory { get; set; }

        public LocalNode(INetworkListenerLoop networkListenerLoop,
            IUPnPPortForwarder portForwarder,
            ILocalIpAddressDiscoverer localIpAddressDiscoverer,
            RemoteNode.Factory remoteNodeFactory,
            IPeerTracker peerTracker,
            ClientSettings clientSettings)
        {
            _uPnPPortForwarder = portForwarder;
            _peerTracker = peerTracker;

            _networkListenerLoop = networkListenerLoop;
            _networkListenerLoop.ConnectionEstablished += HandleConnectionEstablished;

            _clientSettings = clientSettings;
            _remoteNodeFactory = remoteNodeFactory;

            if (_clientSettings.BlockChainNetSettings.Net == "LocalNet")
            {
                _seedNodeEndPoint = new IPEndPoint(IPAddress.Parse(localIpAddressDiscoverer.GetLocalIpAddress()), _clientSettings.ListenPort);
            }
            int port = _clientSettings.BlockChainNetSettings?.ListenPortOverride ?? _clientSettings.ListenPort;
            LocalAddress = new IPEndPoint(IPAddress.Parse(localIpAddressDiscoverer.GetLocalIpAddress()), port);

            Console.WriteLine($"Started Listening on {LocalAddress.Address}:{LocalAddress.Port}");
            Console.WriteLine();
        }

        public IPEndPoint LocalAddress { get; private set; }

        public void Start()
        {
            if (_clientSettings.NodeType != "SeedNode")
            {
                Task.Delay(2000).Wait();
                Task.Run(() => ConnectToPeer(_seedNodeEndPoint));
            }
            Task.Run(async () =>
            {
                await _uPnPPortForwarder.ForwardPortIfNatFound();
                _networkListenerLoop.StartListeningForConnections();
            });

            Task.Run(() => RequestMorePeersLoop());
        }


        private void RequestMorePeersLoop()
        {
            while (true)
            {
                if (_peerTracker.NeedsMoreConnectedPeers)
                {
                    foreach (var remoteNode in _peerTracker.GetConnectedPeers())
                    {
                        if (!remoteNode.QueueContainsMessageType<RequestPeerAddressesMessage>())
                        {
                            remoteNode.QueueMessage(new RequestPeerAddressesMessage());
                            var transMessage = new TransactionMessage(new TransferTransaction());
                            remoteNode.QueueMessage(transMessage);
                        }
                    }

                    foreach (var endpoint in _peerTracker.GetNotConnectedEndPoints(Int32.MaxValue, 0))
                    {
                        Task.Run(() => ConnectToPeer(endpoint));
                    }

                    for (int i = 0; i < 50; i++)
                    {
                        Thread.Sleep(100);
                    }
                }
            }
        }

        private void ConnectToPeer(IPEndPoint endpoint)
        {
            TcpClient client = new TcpClient();
            client.Connect(endpoint);

            if (client.Connected)
            {
                OnNodeConnectionEstablished(client);
            }
        }

        private void OnNodeConnectionEstablished(TcpClient client)
        {
            IRemoteNode newNode = _remoteNodeFactory.Invoke(client);
            newNode.ExecuteProtocolHandShake();
        }

        private void HandleConnectionEstablished(object sender, TcpClient client)
        {
            OnNodeConnectionEstablished(client);
        }
    }
}