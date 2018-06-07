using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Sidechains.Features.BlockchainGeneration;

namespace Stratis.FederatedSidechains.IntegrationTests.Common
{
    public static class CoreNodeExtensions
    {
        public static int ApiPort(this CoreNode coreNode)
        {
            return coreNode.FullNode.NodeService<ApiSettings>().ApiPort;
        }
    }

    public static class NodeBuilderExtensions
    {
        public static CoreNode CreatePowPosMiningNode(this NodeBuilder noderBuilder,
            Network network, bool start = false, string agent = "PowPosMining")
        {
            var node = noderBuilder.CreateCustomNode(start, fullNodeBuilder =>
            {
                fullNodeBuilder
                    .UseBlockStore()
                    .UsePosConsensus()
                    .UseMempool()
                    .UseWallet()
                    .AddPowPosMining()
                    .UseApi()
                    .AddRPC()
                    .MockIBD();
            }, network, agent: agent);

            return node;
        }

        public static CoreNode CreatePowPosSidechainApiMiningNode(this NodeBuilder noderBuilder,
            Network network, bool start = false, string agent = "PowPosMining")
        {
            
            var node = noderBuilder.CreateCustomNode(start, fullNodeBuilder =>
            {
                fullNodeBuilder
                    .UseBlockStore()
                    .UsePosConsensus()
                    .UseMempool()
                    .UseWallet()
                    .AddPowPosMining()
                    .UseApi()
                    .UseSidechains()
                    .AddRPC()
                    .MockIBD();
            }, network, agent: agent);

            return node;
        }

        /// <summary>
        /// TODO: Use that method to attribute free ApiPorts automatically
        /// </summary>
        /// <param name="ports"></param>
        private static void FindPorts(int[] ports)
        {
            int i = 0;
            while (i < ports.Length)
            {
                var port = RandomUtils.GetUInt32() % 4000;
                port = port + 10000;
                if (ports.Any(p => p == port))
                    continue;
                try
                {
                    TcpListener l = new TcpListener(IPAddress.Loopback, (int)port);
                    l.Start();
                    l.Stop();
                    ports[i] = (int)port;
                    i++;
                }
                catch (SocketException)
                {
                }
            }
        }
    }
}
