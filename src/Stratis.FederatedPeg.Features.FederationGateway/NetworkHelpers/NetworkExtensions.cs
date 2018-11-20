﻿using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Networks;
using Stratis.Sidechains.Networks;

namespace Stratis.FederatedPeg.Features.FederationGateway.NetworkHelpers
{
    public enum Chain
    {
        Mainchain,
        Sidechain
    }

    /// <summary>
    /// Network helper extensions for identifying a sidechain or mainchain network.
    /// </summary>
    public static class NetworkExtensions
    {
        public static readonly List<string> MainChainNames = new List<Network> {
            new StratisMain(), new StratisTest(), new StratisRegTest(),
            new BitcoinMain(), new BitcoinTest(), new BitcoinRegTest()
        }.Select(n => n.Name.ToLower()).ToList();

        public static Chain ToChain(this Network network)
        {
            return MainChainNames.Contains(network.Name.ToLower()) ? Chain.Mainchain : Chain.Sidechain;
        }

        public static Network ToCounterChainNetwork(this Network network)
        {
            if (network.Name.ToLower() == MainChainNames[0]) return ApexNetworks.Apex.Mainnet();
            if (network.Name.ToLower() == MainChainNames[1]) return ApexNetworks.Apex.Testnet();
            if (network.Name.ToLower() == MainChainNames[2]) return ApexNetworks.Apex.Regtest();
            if (network.Name.ToLower() == ApexNetworks.Apex.Mainnet().Name.ToLower()) return new StratisMain();
            if (network.Name.ToLower() == ApexNetworks.Apex.Testnet().Name.ToLower()) return new StratisTest();
            if (network.Name.ToLower() == ApexNetworks.Apex.Regtest().Name.ToLower()) return new StratisRegTest();
            throw new System.ArgumentException("Unknown network.");
        }
    }
}