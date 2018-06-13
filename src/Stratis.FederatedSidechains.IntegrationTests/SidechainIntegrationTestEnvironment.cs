using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.GeneralPurposeWallet;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.FederatedPeg;
using Stratis.FederatedPeg.Features.FederationGateway;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Stratis.FederatedSidechains.IntegrationTests
{
    public class GatewayIntegrationTestEnvironment
    {
        Dictionary<Chain, Network> Networks { get; }
        Dictionary<Chain, Mnemonic> ChainMnemonics { get; }
        public List<NodeKey> FederationNodeKeys { get; private set; }
        public List<FederationMemberKey> FederationMemberKeys { get; private set; }
        public Dictionary<FederationMemberKey, Mnemonic> FederationMembersMnemonics { get; private set; }
        public Script RedeemScript { get; private set; }
        public Dictionary<NodeKey, GeneralPurposeAccount> GpAccountsByKey { get; }

        private readonly NodeBuilder nodeBuilder;
        private readonly Dictionary<NodeKey, CoreNode> nodesByKey;

        public readonly int FederationMemberCount;
        public int QuorumSize => FederationMemberCount / 2 + 1;

        public GatewayIntegrationTestEnvironment(NodeBuilder nodeBuilder, Network mainchainNetwork, Network sidechainNetwork, int federationMemberCount = 3)
        {
            ChainMnemonics = new Dictionary<Chain, Mnemonic>();
            Networks = new Dictionary<Chain, Network>
            {
                {Chain.Mainchain, mainchainNetwork},
                {Chain.Sidechain, sidechainNetwork }
            };

            this.nodeBuilder = nodeBuilder;
            FederationMemberCount = federationMemberCount;
            BuildMnemonics();
            BuildRedeemScript();
            BuildFederationMembersNodeKeys();
            BuildFederationNodes();
            BuildGeneralPurposeWallets();
        }

        private void BuildMnemonics()
        {
            foreach (var federationMemberKey in FederationMemberKeys)
            {
                var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
                FederationMembersMnemonics.Add(federationMemberKey, mnemonic);
            }
            ChainMnemonics[Chain.Mainchain] = new Mnemonic(Wordlist.English, WordCount.Twelve);
            ChainMnemonics[Chain.Sidechain] = new Mnemonic(Wordlist.English, WordCount.Twelve);
        }

        private void BuildRedeemScript()
        {
            this.RedeemScript = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(QuorumSize,
                FederationMembersMnemonics.Values
                    .Select(m => m.DeriveExtKey().PrivateKey.PubKey).ToArray());
        }

        private void BuildFederationNodes()
        {
            foreach (var key in FederationNodeKeys)
            {
                BuildFederationNode(key);
            }
        }

        private CoreNode BuildFederationNode(NodeKey key)
        {
            var addParametersAction = new Action<CoreNode>(n =>
            {
                n.ConfigParameters.Add("membername", key.AsFederationMemberKey().Name);
                n.ConfigParameters.Add("apiport", key.SelfApiPort.ToString());
                n.ConfigParameters.Add("counterchainapiport", key.CounterChainApiPort.ToString());
                n.ConfigParameters.Add("redeemscript", this.RedeemScript.ToString());
                n.ConfigParameters.Add("federationips", 
                    string.Join(",", Enumerable.Range(0, FederationMemberCount).Select(i => "127.0.0.1")));
            });
            TestHelper.BuildStartAndRegisterNode(nodeBuilder,
                fullNodeBuilder => fullNodeBuilder
                    .UsePosConsensus()
                    .UseBlockStore()
                    .UseMempool()
                    .UseWallet()
                    .AddPowPosMining()
                    .AddFederationGateway()
                    .UseGeneralPurposeWallet()
                    .UseBlockNotification()
                    .UseApi()
                    .AddRPC(),
                key, nodesByKey, Networks[key.Chain], addParametersAction);
            TestHelper.ConnectNodeToOtherNodesInTest(key, nodesByKey);
            return nodesByKey[key];
        }

        private void BuildGeneralPurposeWallets()
        {
            foreach (var key in FederationNodeKeys)
            {
                //todo: change that when GeneralPurposeWallets are ready again
                //var generalWalletManager = nodesByKey[key].FullNode.NodeService<IGeneralPurposeWalletManager>();
                //generalWalletManager.CreateWallet(NamingConstants.MultisigPassword, NamingConstants.MultisigWallet)
                GpAccountsByKey.Add(key, null);
            }
        }

        private void BuildFederationMembersNodeKeys()
        {
            FederationNodeKeys = new List<NodeKey>();
            foreach (var chain in Enum.GetValues(typeof(Chain)))
            {
                var keys = Enumerable.Range(0, FederationMemberCount)
                    .Select(i => new NodeKey { Chain = (Chain)chain, Role = NodeRole.Federation, Index = i }).ToList();
                FederationNodeKeys.AddRange(keys);
            }

            FederationMemberKeys = FederationNodeKeys.Select(n => n.AsFederationMemberKey()).Distinct().ToList();

            FederationNodeKeys.Count.Should().Be(FederationMemberCount * 2);
            FederationMemberKeys.Count.Should().Be(FederationMemberCount);
        }
        
        public BitcoinAddress GetMultisigAddress(Chain chain)
        {
            return RedeemScript.Hash.GetAddress(Networks[chain]);
        }

        public Script GetMultisigPubKey(Chain chain)
        {
            return GetMultisigAddress(chain).ScriptPubKey;
        }
    }
}

