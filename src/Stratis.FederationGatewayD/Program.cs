﻿using System;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.GeneralPurposeWallet;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.Networks.Apex;

namespace Stratis.FederationGatewayD
{
    class Program
    {
        private const string MainchainArgument = "-mainchain";
        private const string SidechainArgument = "-sidechain";

        static void Main(string[] args)
        {
            RunFederationGatewayAsync(args).Wait();
        }

        public static async Task RunFederationGatewayAsync(string[] args)
        {
            try
            {
                var isMainchainNode = args.FirstOrDefault(a => a.ToLower() == MainchainArgument) != null;
                var isSidechainNode = args.FirstOrDefault(a => a.ToLower() == SidechainArgument) != null;
                if(isSidechainNode == isMainchainNode) throw new ArgumentException(
                    $"Gateway node needs to be started specifiying either a {SidechainArgument} or a {MainchainArgument} argument");

                var network = isMainchainNode ? Network.StratisTest : ApexNetwork.Test; 
                var nodeSettings = new NodeSettings(network, ProtocolVersion.ALT_PROTOCOL_VERSION, args: args);

                var node = isMainchainNode
                    ? new FullNodeBuilder()
                        .UseNodeSettings(nodeSettings)
                        .UseBlockStore()
                        .UsePosConsensus()
                        .UseMempool()
                        .UseWallet()
                        .UseGeneralPurposeWallet()
                        .UseTransactionNotification()
                        .UseBlockNotification()
                        .AddPowPosMining()
                        .AddFederationGateway()
                        .UseApi()
                        .AddRPC()
                        .Build()
                    : new FullNodeBuilder()
                        .UseNodeSettings(nodeSettings)
                        .UseBlockStore()
                        .UsePowConsensus()
                        .UseMempool()
                        .UseWallet()
                        .UseGeneralPurposeWallet()
                        .UseTransactionNotification()
                        .UseBlockNotification()
                        .AddMining()
                        .AddFederationGateway()
                        .UseApi()
                        .AddRPC()
                        .Build();

                if (node != null)
                    await node.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem initializing the node. Details: '{0}'", ex.Message);
            }
        }
    }
}
