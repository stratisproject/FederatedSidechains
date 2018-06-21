﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Utilities;
using Stratis.Sidechains.Networks;

namespace Stratis.SidechainD
{
    /// <summary>
    /// Starts a console app that includes the sidechain network parameters and that should be distributed to the sidechain users.
    /// </summary>
    class Program
    {
        public static void Main(string[] args)
        {
            MainAsync(args).Wait();
        }

        public static async Task MainAsync(string[] args)
        {
            try
            {
                if (!args.Any(a => a.Contains("apiport")))
                {
                    // TEMP set the default port to 38225 if it isn't set.
                    args = args.Concat(new[] { "apiport=38225" }).ToArray();
                }

                NodeSettings nodeSettings = new NodeSettings(ApexNetwork.Test, protocolVersion: ProtocolVersion.ALT_PROTOCOL_VERSION, args: args);

                string[] seedNodes = { "104.211.178.243", "51.144.35.218", "65.52.5.149", "51.140.231.125", "13.70.81.5" };
                ApexNetwork.Test.SeedNodes.AddRange(ConvertToNetworkAddresses(seedNodes, ApexNetwork.Test.DefaultPort).ToList());

                var node = new FullNodeBuilder()
                    .UseNodeSettings(nodeSettings)
                    .UseBlockStore()
                    .UsePowConsensus()
                    .UseMempool()
                    .AddRPC()
                    .UseWallet()
                    .UseApi()
                    .Build();

                if (node != null)
                    await node.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem initializing the node. Details: '{0}'", ex.Message);
            }
        }

        protected static IEnumerable<NetworkAddress> ConvertToNetworkAddresses(string[] seeds, int defaultPort)
        {
            var rand = new Random();
            TimeSpan oneWeek = TimeSpan.FromDays(7);

            foreach (string seed in seeds)
            {
                // It'll only connect to one or two seed nodes because once it connects,
                // it'll get a pile of addresses with newer timestamps.
                // Seed nodes are given a random 'last seen time' of between one and two weeks ago.
                yield return new NetworkAddress
                {
                    Time = DateTime.UtcNow - (TimeSpan.FromSeconds(rand.NextDouble() * oneWeek.TotalSeconds)) - oneWeek,
                    Endpoint = Utils.ParseIpEndpoint(seed, defaultPort)
                };
            }
        }
    }
}
