﻿using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Sidechains.Features.BlockchainGeneration;

namespace Stratis.FederatedPeg
{
    /// <summary>
    /// Network helper extensions for identifying a sidechain or mainchain network.
    /// </summary>
    public static class NetworkExtensions
    {
        public static readonly List<string> MainChainNames = new List<Network> {
            Network.StratisMain, Network.StratisTest, Network.StratisRegTest,
            Network.Main, Network.TestNet, Network.RegTest
        }.Select(n => n.Name.ToLower()).ToList();

        /// <summary>
        /// Returns whether we are on a sidechain or a mainchain network.
        /// </summary>
        /// <param name="network">The network to examine.</param>
        /// <returns>This function tests for a sidechain and returns mainchain for any non sidechain network.</returns>
        public static Chain ToChain(this Network network)
        {
            return MainChainNames.Contains(network.Name.ToLower()) ? Chain.Mainchain : Chain.Sidechain;
        }

        /// <summary>
        /// Returns the network's counter chain network.
        /// </summary>
        /// <param name="network">The network to examine.</param>
        /// <returns></returns>
        public static Network ToCounterChainNetwork(this Network network)
        {
            if (network == Network.StratisMain) return SidechainNetwork.SidechainMain;
            if (network == Network.StratisTest) return SidechainNetwork.SidechainTest;
            if (network == Network.StratisRegTest) return SidechainNetwork.SidechainRegTest;
            if (network == SidechainNetwork.SidechainMain) return Network.StratisMain;
            if (network == SidechainNetwork.SidechainTest) return Network.StratisTest;
            if (network == SidechainNetwork.SidechainRegTest) return Network.StratisRegTest;
            throw new System.ArgumentException("Unknown network.");
        }
    }
}