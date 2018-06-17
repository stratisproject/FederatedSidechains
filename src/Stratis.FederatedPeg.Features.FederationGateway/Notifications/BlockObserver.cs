﻿using NBitcoin;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.Notifications
{
    /// <summary>
    /// Observer that passes notifications indicating the arrival of new <see cref="Block"/>s
    /// onto the CrossChainTransactionMonitor.
    /// </summary>
    internal sealed class BlockObserver : SignalObserver<Block>
    {
        // The monitor we pass the new blocks onto.
        private readonly ICrossChainTransactionMonitor crossChainTransactionMonitor;

        private readonly IFederationWalletSyncManager walletSyncManager;

        /// <summary>
        /// Initialize the block observer with the wallet manager and the cross chain monitor.
        /// </summary>
        /// <param name="crossChainTransactionMonitor"></param>
        public BlockObserver(IFederationWalletSyncManager walletSyncManager, ICrossChainTransactionMonitor crossChainTransactionMonitor)
        {
            Guard.NotNull(walletSyncManager, nameof(walletSyncManager));
            Guard.NotNull(crossChainTransactionMonitor, nameof(crossChainTransactionMonitor));

            this.walletSyncManager = walletSyncManager;
            this.crossChainTransactionMonitor = crossChainTransactionMonitor;
        }

        /// <summary>
        /// When a block is received it is passed to the monitor.
        /// </summary>
        /// <param name="block">The new block.</param>
        protected override void OnNextCore(Block block)
        {
            crossChainTransactionMonitor?.ProcessBlock(block);
            walletSyncManager?.ProcessBlock(block);
        }
    }
}