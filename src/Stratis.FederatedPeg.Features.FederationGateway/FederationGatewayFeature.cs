﻿using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.Controllers;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Payloads;
using Stratis.FederatedPeg.Features.FederationGateway.SourceChain;
using Stratis.FederatedPeg.Features.FederationGateway.SourceChain.Auditors;
using Stratis.FederatedPeg.Features.FederationGateway.TargetChain;
using Stratis.FederatedPeg.Features.FederationGateway.Wallet;
using BlockObserver = Stratis.FederatedPeg.Features.FederationGateway.Notifications.BlockObserver;

[assembly: InternalsVisibleTo("Stratis.FederatedPeg.Features.FederationGateway.Tests")]
[assembly: InternalsVisibleTo("Stratis.FederatedPeg.IntegrationTests")]

//todo: this is pre-refactoring code
//todo: ensure no duplicate or fake withdrawal or deposit transactions are possible (current work underway)

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    internal class FederationGatewayFeature : FullNodeFeature
    {
        private readonly ITransferSource crossChainTransactionMonitor;

        private readonly Signals signals;

        private IDisposable blockSubscriberDisposable;

        private IDisposable transactionSubscriberDisposable;

        private readonly IConnectionManager connectionManager;

        private readonly FederationGatewaySettings federationGatewaySettings;

        private IFullNode fullNode;

        private readonly ILoggerFactory loggerFactory;

        private readonly IFederationWalletManager federationWalletManager;

        private readonly IFederationWalletSyncManager walletSyncManager;

        private readonly ConcurrentChain chain;

        private readonly Network network;

        private readonly ITransferSourceManager sourceChainSessionManager;
        
        private readonly ITargetTransferManager counterChainSessionManager;

        public FederationGatewayFeature(ILoggerFactory loggerFactory, 
            ITransferSource crossChainTransactionMonitor, 
            Signals signals,
            IConnectionManager connectionManager,
            FederationGatewaySettings federationGatewaySettings, 
            IFullNode fullNode,
            IFederationWalletManager federationWalletManager,
            IFederationWalletSyncManager walletSyncManager,
            Network network,
            ConcurrentChain chain,
            ITransferSourceManager sourceChainSessionManager, 
            ITargetTransferManager counterChainSessionManager,
            INodeStats nodeStats)
        {
            this.loggerFactory = loggerFactory;
            this.crossChainTransactionMonitor = crossChainTransactionMonitor;
            this.signals = signals;
            this.connectionManager = connectionManager;
            this.federationGatewaySettings = federationGatewaySettings;
            this.fullNode = fullNode;
            this.chain = chain;
            this.federationWalletManager = federationWalletManager;
            this.walletSyncManager = walletSyncManager;
            this.network = network;

            this.counterChainSessionManager = counterChainSessionManager;
            this.sourceChainSessionManager = sourceChainSessionManager;

            // add our payload
            var payloadProvider = (PayloadProvider)this.fullNode.Services.ServiceProvider.GetService(typeof(PayloadProvider));
            payloadProvider.AddPayload(typeof(RequestPartialTransactionPayload));

            nodeStats.RegisterStats(this.AddComponentStats, StatsType.Component);
            nodeStats.RegisterStats(this.AddInlineStats, StatsType.Inline, 800);
        }

        public override async Task InitializeAsync()
        {
            // Subscribe to receiving blocks and transactions.
            this.blockSubscriberDisposable = this.signals.SubscribeForBlocksConnected(new BlockObserver(this.walletSyncManager, this.crossChainTransactionMonitor));
            this.transactionSubscriberDisposable = this.signals.SubscribeForTransactions(new Notifications.TransactionObserver(this.walletSyncManager));
            
            this.crossChainTransactionMonitor.Initialize(federationGatewaySettings);
            this.sourceChainSessionManager.Initialize();

            this.federationWalletManager.Start();
            this.walletSyncManager.Start();

            // Connect the node to the other federation members.
            foreach (var federationMemberIp in federationGatewaySettings.FederationNodeIpEndPoints)
            {
                this.connectionManager.AddNodeAddress(federationMemberIp);
            }
            
            var networkPeerConnectionParameters = this.connectionManager.Parameters;
            networkPeerConnectionParameters.TemplateBehaviors.Add(new PartialTransactionsBehavior(this.loggerFactory, this.crossChainTransactionMonitor, this.federationWalletManager, this.counterChainSessionManager, this.network, this.federationGatewaySettings));
        }

        public override void Dispose()
        {
            this.blockSubscriberDisposable.Dispose();
            this.transactionSubscriberDisposable.Dispose();
            this.crossChainTransactionMonitor.Dispose();
            this.sourceChainSessionManager.Dispose();
        }

        /// <inheritdoc />
        public void AddInlineStats(StringBuilder benchLogs)
        {
            if (federationWalletManager == null) return;
            int height = this.federationWalletManager.LastBlockHeight();
            ChainedHeader block = this.chain.GetBlock(height);
            uint256 hashBlock = block == null ? 0 : block.HashBlock;

            var federationWallet = this.federationWalletManager.GetWallet();
            benchLogs.AppendLine("Federation Wallet.Height: ".PadRight(LoggingConfiguration.ColumnLength + 1) +
                                 (federationWallet != null ? height.ToString().PadRight(8) : "No Wallet".PadRight(8)) +
                                 (federationWallet != null ? (" Federation Wallet.Hash: ".PadRight(LoggingConfiguration.ColumnLength - 1) + hashBlock) : string.Empty));
        }

        public void AddComponentStats(StringBuilder benchLog)
        {
            benchLog.AppendLine();
            benchLog.AppendLine("====== Federation Wallet ======");

            var items = this.federationWalletManager.GetSpendableTransactionsInWallet(1);
            benchLog.AppendLine("Federation Wallet: ".PadRight(LoggingConfiguration.ColumnLength) + " Confirmed balance: " + new Money(items.Sum(s => s.Transaction.Amount)).ToString());
            benchLog.AppendLine();
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderSidechainRuntimeFeatureExtension
    {
        public static IFullNodeBuilder AddFederationGateway(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<FederationGatewayFeature>("federationgateway");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<FederationGatewayFeature>()
                    .DependOn<BlockNotificationFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<FederationGatewayController>();
                        services.AddSingleton<FederationGatewaySettings>();
                        services.AddSingleton<ITransferSource, TransferSource>();
                        services.AddSingleton<ITransferAuditor, JsonTransferAuditor>();
                        services.AddSingleton<ITransferSourceManager, TransferSourceManager>();
                        services.AddSingleton<ITargetTransferManager, TargetTransferManager>();
                        services.AddSingleton<IFederationWalletSyncManager, FederationWalletSyncManager>();
                        services.AddSingleton<IFederationWalletTransactionHandler, FederationWalletTransactionHandler>();
                        services.AddSingleton<IFederationWalletManager, FederationWalletManager>();
                        services.AddSingleton<FederationWalletController>();
                    });
            });
            return fullNodeBuilder;
        }
    }
}
