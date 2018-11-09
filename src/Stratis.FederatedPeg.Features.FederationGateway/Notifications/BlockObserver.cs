using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.Notifications
{
    /// <summary>
    /// Observer that passes notifications indicating the arrival of new <see cref="Block"/>s
    /// onto the CrossChainTransactionMonitor.
    /// </summary>
    public class BlockObserver : SignalObserver<ChainedHeaderBlock>
    {
        // The monitor we pass the new blocks onto.
        private readonly ICrossChainTransactionMonitor crossChainTransactionMonitor;

        private readonly IFederationWalletSyncManager walletSyncManager;

        private readonly IMaturedBlockSender maturedBlockSender;

        private readonly IMaturedBlockDepositsProcessor maturedBlockDepositsProcessor;

        private readonly IBlockTipSender blockTipSender;

        private readonly ConcurrentChain chain;

        /// <summary>
        /// Initialize the block observer with the wallet manager and the cross chain monitor.
        /// </summary>
        /// <param name="walletSyncManager">The wallet sync manager to pass new incoming blocks to.</param>
        /// <param name="crossChainTransactionMonitor">The cross-chain transaction monitor to pass new incoming blocks to.</param>
        /// <param name="maturedBlockDepositsProcessor">TODO.</param>
        /// <param name="fullNode">Full node used to get rewind the chain.</param>
        /// <param name="maturedBlockSender">Service responsible for publishing newly matured blocks.</param>
        /// /// <param name="blockTipSender">Service responsible for publishing the block tip.</param>
        public BlockObserver(IFederationWalletSyncManager walletSyncManager,
                             ICrossChainTransactionMonitor crossChainTransactionMonitor,
                             IFullNode fullNode,
                             IMaturedBlockSender maturedBlockSender,
                             IBlockTipSender blockTipSender,
                             IMaturedBlockDepositsProcessor maturedBlockDepositsProcessor)
        {
            Guard.NotNull(walletSyncManager, nameof(walletSyncManager));
            Guard.NotNull(crossChainTransactionMonitor, nameof(crossChainTransactionMonitor));
            Guard.NotNull(fullNode, nameof(fullNode));
            Guard.NotNull(maturedBlockSender, nameof(maturedBlockSender));
            Guard.NotNull(blockTipSender, nameof(blockTipSender));
            Guard.NotNull(maturedBlockDepositsProcessor, nameof(maturedBlockDepositsProcessor));

            this.walletSyncManager = walletSyncManager;
            this.crossChainTransactionMonitor = crossChainTransactionMonitor;
            this.maturedBlockSender = maturedBlockSender;
            this.maturedBlockDepositsProcessor = maturedBlockDepositsProcessor;
            this.chain = fullNode.NodeService<ConcurrentChain>();
            this.blockTipSender = blockTipSender;
        }

        /// <summary>
        /// When a block is received it is passed to the monitor.
        /// </summary>
        /// <param name="chainedHeaderBlock">The new block.</param>
        protected override void OnNextCore(ChainedHeaderBlock chainedHeaderBlock)
        {
            this.crossChainTransactionMonitor.ProcessBlock(chainedHeaderBlock.Block);
            this.walletSyncManager.ProcessBlock(chainedHeaderBlock.Block);

            // todo: persist the last seen block height in database
            // todo: save these deposits in local database

            MaturedBlockDepositsModel maturedBlockDeposits = 
                this.maturedBlockDepositsProcessor.ExtractMaturedBlockDeposits(chainedHeaderBlock);

            if (maturedBlockDeposits != null)
            {
                this.maturedBlockSender.SendMaturedBlockDepositsAsync(maturedBlockDeposits).ConfigureAwait(false);
            }

            this.blockTipSender.SendBlockTipAsync(this.ExtractBlockTip(chainedHeaderBlock.ChainedHeader));
        }

        private BlockTipModel ExtractBlockTip(ChainedHeader chainedHeader)
        {
            return new BlockTipModel(chainedHeader.HashBlock, chainedHeader.Height);
        }
    }
}