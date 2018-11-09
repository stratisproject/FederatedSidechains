using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.SourceChain
{
    /// <summary>
    /// Processes a <see cref="ChainedHeaderBlock"/>, by selecting a new matured block and extracting any block deposits.
    /// </summary>
    public class MaturedBlockDepositsProcessor: IMaturedBlockDepositsProcessor
    {
        private readonly IDepositExtractor depositExtractor;
        private readonly ConcurrentChain chain;
        private readonly uint minimumDepositConfirmations;

        /// <summary>
        /// Constructor initialization.
        /// </summary>
        /// <param name="depositExtractor">The component used to extract the deposits from the blocks appearing on chain.</param>
        /// <param name="federationGatewaySettings">The settings used to run this federation node.</param>
        public MaturedBlockDepositsProcessor(
            IDepositExtractor depositExtractor,
            IFederationGatewaySettings federationGatewaySettings)
        {
            Guard.NotNull(depositExtractor, nameof(depositExtractor));
            Guard.NotNull(federationGatewaySettings, nameof(federationGatewaySettings));

            this.depositExtractor = depositExtractor;
            this.minimumDepositConfirmations = federationGatewaySettings.MinimumDepositConfirmations;
        }

        public MaturedBlockDepositsModel ExtractMaturedBlockDeposits(ChainedHeaderBlock latestPublishedBlock)
        {
            ChainedHeader newlyMaturedBlock = this.GetNewlyMaturedBlock(latestPublishedBlock);

            if (newlyMaturedBlock == null) return null;

            var maturedBlock = new MaturedBlockModel()
            {
                BlockHash = newlyMaturedBlock.HashBlock,
                BlockHeight = newlyMaturedBlock.Height
            };

            IReadOnlyList<IDeposit> deposits = 
                this.depositExtractor.ExtractDepositsFromBlock(newlyMaturedBlock.Block, newlyMaturedBlock.Height);

            var maturedBlockDeposits = new MaturedBlockDepositsModel(maturedBlock, deposits);

            return maturedBlockDeposits;
        }

        private ChainedHeader GetNewlyMaturedBlock(ChainedHeaderBlock latestPublishedBlock)
        {
            var newMaturedHeight = latestPublishedBlock.ChainedHeader.Height - (int)this.minimumDepositConfirmations;

            if (newMaturedHeight < 0) return null;

            ChainedHeader newMaturedBlock = this.chain.GetBlock(newMaturedHeight);

            return newMaturedBlock;
        }
    }
}
