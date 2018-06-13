﻿using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.FederatedPeg;
using Stratis.Sidechains.Networks;
using System;
using System.Collections.Generic;
using System.Linq;
using Stratis.Bitcoin.Tests.Common.TestFramework;
using Xunit;
using Xunit.Abstractions;
// Disable warnings about "this" qualifier to make the Specification more readable
// ReSharper disable ArrangeThisQualifier

namespace Stratis.FederatedSidechains.IntegrationTests.FederationGateway
{
    public partial class CrossChainTransfers : BddSpecification
    {
        private readonly ITestOutputHelper output;
        private readonly Dictionary<NodeKey, CoreNode> nodesByKey;
        private readonly Dictionary<NodeKey, HdAccount> hdAccountsByKey;
        private readonly NodeBuilder nodeBuilder;
        private readonly SharedSteps sharedSteps;
        private readonly Money moneyFromMining = new Money(5000, MoneyUnit.BTC);
        private readonly Money moneyFromMainchainToSidechain = new Money(1001, MoneyUnit.BTC);
        private readonly Money moneyFromSidechainToMainchain = new Money(402, MoneyUnit.BTC);
        private readonly Network mainchainNetwork;
        private readonly Network sidechainNetwork;

        private GatewayIntegrationTestEnvironment gatewayEnvironment;

        public CrossChainTransfers(ITestOutputHelper output) : base(output)
        {
            this.output = output;
            nodesByKey = new Dictionary<NodeKey, CoreNode>();
            nodeBuilder = NodeBuilder.Create(this.CurrentTest.DisplayName);
            this.mainchainNetwork = Network.StratisRegTest;
            this.sidechainNetwork = ApexNetwork.RegTest;
            sharedSteps = new SharedSteps();
        }

        protected override void BeforeTest() { }

        protected override void AfterTest()
        {
            nodeBuilder?.Dispose();
        }

        [Fact]
        public void PerformCrossChainTransfer()
        {
            Given(a_mainchain_node_with_funded_account);
            And(a_sidechain_node_with_an_account);
            And(a_federation_with_general_purpose_accounts);

            When(the_mainchain_account_transfers_money_to_the_sidechain_account);
            And(the_nodes_on_each_chains_are_synced);

            Then(the_mainchain_wallet_should_have_sent_the_first_transfer);
            Then(the_mainchain_federation_should_have_received_the_first_transfer);
            And(the_sidechain_federation_should_have_sent_the_first_transfer);
            And(the_sidechain_wallet_should_have_received_the_first_transfer);

            When(the_sidechain_account_transfers_money_to_the_mainchain_account);
            And(the_nodes_on_each_chains_are_synced);

            Then(the_sidechain_wallet_should_have_sent_the_second_transfer);
            And(the_sidechain_federation_should_have_received_the_second_transfer);
            And(the_mainchain_federation_should_have_sent_the_second_transfer);
            And(the_sidechain_wallet_should_have_received_the_first_transfer);
        }

        public void a_mainchain_node_with_funded_account()
        {
            var nodeKey = new NodeKey() { Chain = Chain.Mainchain, Role = NodeRole.Wallet };
            var buildNodeAction = new Action<IFullNodeBuilder>(fullNodeBuilder =>
                fullNodeBuilder
                    .UseBlockStore()
                    .UsePosConsensus()
                    .UseMempool()
                    .UseWallet()
                    .AddPowPosMining()
                    .UseApi()
                    .AddRPC()
                );
            
            TestHelper.BuildStartAndRegisterNode(nodeBuilder, buildNodeAction, nodeKey, nodesByKey, mainchainNetwork);
            var node = nodesByKey[nodeKey];

            var account = CreateAndRegisterHdAccount(node, nodeKey);

            sharedSteps.MinePremineBlocks(node, nodeKey.WalletName, account.Name, nodeKey.Password);
            sharedSteps.MineBlocks(100, node, account.Name, nodeKey.WalletName, nodeKey.Password);

            //todo: check that we got this.moneyFromMining after the mining
        }

        public void a_sidechain_node_with_an_account()
        {
            var nodeKey = new NodeKey() { Chain = Chain.Sidechain, Role = NodeRole.Wallet };
            var buildNodeAction = new Action<IFullNodeBuilder>(fullNodeBuilder =>
                fullNodeBuilder
                    .UseBlockStore()
                    .UsePosConsensus()
                    .UseMempool()
                    .UseWallet()
                    .AddPowPosMining()
                    .UseApi()
                    .AddRPC()
            );

            TestHelper.BuildStartAndRegisterNode(nodeBuilder, buildNodeAction, nodeKey, nodesByKey, sidechainNetwork);
        }

        public void a_federation_with_general_purpose_accounts()
        {
            this.gatewayEnvironment = new GatewayIntegrationTestEnvironment(nodeBuilder,
                mainchainNetwork: mainchainNetwork,
                sidechainNetwork: sidechainNetwork);
        }

        public void the_mainchain_account_transfers_money_to_the_sidechain_account()
        {
            var senderNodeKey = new NodeKey() { Chain = Chain.Mainchain, Role = NodeRole.Wallet };
            var receiverNodeKey = new NodeKey() { Chain = Chain.Sidechain, Role = NodeRole.Wallet };
            CreateAndSendCrossChainTransaction(receiverNodeKey, senderNodeKey, moneyFromMainchainToSidechain);
        }

        public void the_nodes_on_each_chains_are_synced()
        {
            var nodesGroupedByChain = nodesByKey.GroupBy(p => p.Key.Chain, p => p.Value).ToList();
            nodesGroupedByChain.ForEach(g => sharedSteps.WaitForNodeToSync(g.ToArray()));
            nodesGroupedByChain.ForEach(g =>
                output.WriteLine($"chain {g.Key} synced to heigth {g.First().FullNode.Chain.Height}"));
        }

        public void the_mainchain_wallet_should_have_sent_the_first_transfer()
        {
            CheckHdWalletBalance(Chain.Sidechain, moneyFromMining - moneyFromMainchainToSidechain);
        }

        public void the_mainchain_federation_should_have_received_the_first_transfer()
        {
            CheckGpWalletBalance(Chain.Mainchain, moneyFromMainchainToSidechain);
        }

        public void the_sidechain_federation_should_have_sent_the_first_transfer()
        {
            CheckGpWalletBalance(Chain.Sidechain, sidechainNetwork.GenesisReward - moneyFromMainchainToSidechain);
        }

        public void the_sidechain_wallet_should_have_received_the_first_transfer()
        {
            CheckHdWalletBalance(Chain.Sidechain, moneyFromMainchainToSidechain);
        }

        public void the_sidechain_account_transfers_money_to_the_mainchain_account()
        {
            var senderNodeKey = new NodeKey() { Chain = Chain.Sidechain, Role = NodeRole.Wallet };
            var receiverNodeKey = new NodeKey() { Chain = Chain.Mainchain, Role = NodeRole.Wallet };
            CreateAndSendCrossChainTransaction(receiverNodeKey, senderNodeKey, moneyFromSidechainToMainchain);
        }

        public void the_sidechain_wallet_should_have_sent_the_second_transfer()
        {
            CheckHdWalletBalance(Chain.Sidechain, moneyFromMainchainToSidechain - moneyFromSidechainToMainchain);
        }

        public void the_sidechain_federation_should_have_received_the_second_transfer()
        {
            CheckGpWalletBalance(Chain.Sidechain, sidechainNetwork.GenesisReward - moneyFromMainchainToSidechain + moneyFromSidechainToMainchain);
        }

        public void the_mainchain_federation_should_have_sent_the_second_transfer()
        {
            CheckGpWalletBalance(Chain.Mainchain, moneyFromMainchainToSidechain - moneyFromSidechainToMainchain);
        }
        public void the_mainchain_wallet_should_have_received_the_second_transfer()
        {
            CheckHdWalletBalance(Chain.Sidechain, moneyFromMining - moneyFromMainchainToSidechain + moneyFromSidechainToMainchain);
        }

        private void CheckGpWalletBalance(Chain chain, Money expectedAmount)
        {
            gatewayEnvironment.GpAccountsByKey.Where(p => p.Key.Chain == chain).ToList()
                .ForEach(p =>
                    p.Value.GetSpendableAmount(true).ConfirmedAmount
                        .Should().Be(expectedAmount));
        }

        private void CheckHdWalletBalance(Chain chain, Money expectedAmount)
        {
            var receiverNodeKey = new NodeKey() {Chain = chain, Role = NodeRole.Wallet};
            hdAccountsByKey[receiverNodeKey].GetSpendableAmount().ConfirmedAmount
                .Should().Be(expectedAmount);
        }

        public HdAccount CreateAndRegisterHdAccount(CoreNode node, NodeKey nodeKey)
        {
            var walletManager = node.FullNode.WalletManager();
            var mnemonic = walletManager.CreateWallet(nodeKey.WalletName, nodeKey.Password);
            var account = walletManager.GetAccounts(nodeKey.WalletName).First();
            hdAccountsByKey.Add(nodeKey, account);
            return account;
        }

        private void CreateAndSendCrossChainTransaction(NodeKey receiverNodeKey, NodeKey senderNodeKey, Money amount)
        {
            var receiverAddress = hdAccountsByKey[receiverNodeKey].GetFirstUnusedReceivingAddress();
            
            var accountReference = new WalletAccountReference(senderNodeKey.WalletName, NamingConstants.AccountZero);
            var transactionBuildContext = new Bitcoin.Features.Wallet.TransactionBuildContext(
                accountReference,
                new List<Bitcoin.Features.Wallet.Recipient>()
                {
                    new Bitcoin.Features.Wallet.Recipient()
                    {
                        Amount = amount,
                        ScriptPubKey = gatewayEnvironment.GetMultisigPubKey(Chain.Mainchain)
                    }
                },
                walletPassword: senderNodeKey.Password,
                opReturnData: receiverAddress.Address)
            {
                MinConfirmations = 1,
                TransactionFee = new Money(0.001m, MoneyUnit.BTC),
                Shuffle = true
            };
            var mainchainWalletNode = nodesByKey[senderNodeKey];
            var transaction = mainchainWalletNode.FullNode.WalletTransactionHandler()
                .BuildTransaction(transactionBuildContext);

            mainchainWalletNode.FullNode.NodeService<WalletController>().SendTransaction(
                new Bitcoin.Features.Wallet.Models.SendTransactionRequest(transaction.ToHex()));
        }
    }
}