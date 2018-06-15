﻿using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Signals;
using Stratis.FederatedPeg.Features.FederationGateway.Controllers;
using Stratis.FederatedPeg.Features.FederationGateway.CounterChain;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.MonitorChain;
using Stratis.FederatedPeg.Features.FederationGateway.Wallet;
using BlockObserver = Stratis.FederatedPeg.Features.FederationGateway.Notifications.BlockObserver;

[assembly: InternalsVisibleTo("Stratis.FederatedPeg.Features.FederationGateway.Tests")]
[assembly: InternalsVisibleTo("Stratis.FederatedPeg.IntegrationTests")]

//todo: this is pre-refactoring code
//todo: ensure no duplicate or fake withdrawal or deposit transactions are possible (current work underway)

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    internal class FederationGatewayFeature : FullNodeFeature, INodeStats, IFeatureStats
    {
        private readonly ICrossChainTransactionMonitor crossChainTransactionMonitor;

        private readonly Signals signals;

        private IDisposable blockSubscriberDisposable;

        private IDisposable transactionSubscriberDisposable;

        private readonly IConnectionManager connectionManager;

        private FederationGatewaySettings federationGatewaySettings;

        private NodeSettings nodeSettings;

        private IFullNode fullNode;

        private ILoggerFactory loggerFactory;

        private IFederationWalletManager federationWalletManager;

        private readonly IFederationWalletSyncManager walletSyncManager;

        private ConcurrentChain chain;

        private Network network;

        private IMonitorChainSessionManager monitorChainSessionManager;
        
        private ICounterChainSessionManager counterChainSessionManager;

        public FederationGatewayFeature(ILoggerFactory loggerFactory, 
            ICrossChainTransactionMonitor crossChainTransactionMonitor, 
            Signals signals,
            IConnectionManager connectionManager,
            FederationGatewaySettings federationGatewaySettings, 
            NodeSettings nodeSettings, 
            IFullNode fullNode,
            IFederationWalletManager federationWalletManager,
            IFederationWalletSyncManager walletSyncManager,
            Network network,
            ConcurrentChain chain,
            IMonitorChainSessionManager monitorChainSessionManager, 
            ICounterChainSessionManager counterChainSessionManager)
        {
            this.loggerFactory = loggerFactory;
            this.crossChainTransactionMonitor = crossChainTransactionMonitor;
            this.signals = signals;
            this.connectionManager = connectionManager;
            this.federationGatewaySettings = federationGatewaySettings;
            this.nodeSettings = nodeSettings;
            this.fullNode = fullNode;
            this.chain = chain;
            this.federationWalletManager = federationWalletManager;
            this.walletSyncManager = walletSyncManager;
            this.network = network;

            this.counterChainSessionManager = counterChainSessionManager;
            this.monitorChainSessionManager = monitorChainSessionManager;

            // add our payload
            var payloadProvider = this.fullNode.Services.ServiceProvider.GetService(typeof(PayloadProvider)) as PayloadProvider;
            payloadProvider.AddPayload(typeof(RequestPartialTransactionPayload));
        }

        public override void Initialize()
        {
            // subscribe to receiving transactions
            // subscribe to receiving blocks and transactions
            this.blockSubscriberDisposable = this.signals.SubscribeForBlocks(new BlockObserver(this.walletSyncManager));
            this.transactionSubscriberDisposable = this.signals.SubscribeForTransactions(new Notifications.TransactionObserver(this.walletSyncManager));
            this.blockSubscriberDisposable = this.signals.SubscribeForBlocks(new BlockObserver(this.crossChainTransactionMonitor));

            this.crossChainTransactionMonitor.Initialize(federationGatewaySettings);
            this.monitorChainSessionManager.Initialize();

            this.federationWalletManager.Start();
            this.walletSyncManager.Start();

            // Connect the node to the other federation members.
            foreach (var federationMemberIp in federationGatewaySettings.FederationNodeIps)
            {
                this.connectionManager.AddNodeAddress(federationMemberIp);
            }
            
            var networkPeerConnectionParameters = this.connectionManager.Parameters;
            networkPeerConnectionParameters.TemplateBehaviors.Add(new PartialTransactionsBehavior(this.loggerFactory, this.crossChainTransactionMonitor, this.federationWalletManager, this.counterChainSessionManager, this.network, this.federationGatewaySettings ));
        }

        public override void Dispose()
        {
            this.blockSubscriberDisposable.Dispose();
            this.transactionSubscriberDisposable.Dispose();
            this.crossChainTransactionMonitor.Dispose();
            this.monitorChainSessionManager.Dispose();
        }

        /// <inheritdoc />
        public void AddNodeStats(StringBuilder benchLogs)
        {
            FederationWalletManager walletManager = this.federationWalletManager as FederationWalletManager;

            if (walletManager != null)
            {
                int height = walletManager.LastBlockHeight();
                ChainedHeader block = this.chain.GetBlock(height);
                uint256 hashBlock = block == null ? 0 : block.HashBlock;

                benchLogs.AppendLine("Federation Wallet.Height: ".PadRight(LoggingConfiguration.ColumnLength + 1) +
                                     (walletManager.Wallet != null ? height.ToString().PadRight(8) : "No Wallet".PadRight(8)) +
                                     (walletManager.Wallet != null ? (" Federation Wallet.Hash: ".PadRight(LoggingConfiguration.ColumnLength - 1) + hashBlock) : string.Empty));
            }
        }

        public void AddFeatureStats(StringBuilder benchLog)
        {
            var wallet = this.federationWalletManager.GetWallet();

            benchLog.AppendLine();
            benchLog.AppendLine("====== Federation Wallets======");

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
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<FederationGatewayFeature>()
                    .DependOn<BlockNotificationFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<FederationGatewayController>();
                        services.AddSingleton<FederationGatewaySettings>();
                        services.AddSingleton<ICrossChainTransactionMonitor, CrossChainTransactionMonitor>();
                        services.AddSingleton<ICrossChainTransactionAuditor, JsonCrossChainTransactionAuditor>();
                        services.AddSingleton<IMonitorChainSessionManager, MonitorChainSessionManager>();
                        services.AddSingleton<ICounterChainSessionManager, CounterChainSessionManager>();
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
