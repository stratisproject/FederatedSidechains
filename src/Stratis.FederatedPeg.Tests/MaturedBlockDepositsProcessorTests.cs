using System.Collections.Generic;
using NBitcoin;
using NSubstitute;
using Stratis.Bitcoin.Primitives;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class MaturedBlockDepositsProcessorTests
    {
        private readonly IMaturedBlockDepositsProcessor maturedBlockDepositsProcessor;
        private readonly IDepositExtractor depositExtractor;
        private readonly IFederationGatewaySettings federationGatewaySettings;
        private readonly ConcurrentChain chain;

        public MaturedBlockDepositsProcessorTests()
        {
            this.depositExtractor = Substitute.For<IDepositExtractor>();
            this.federationGatewaySettings = Substitute.For<IFederationGatewaySettings>();
            this.maturedBlockDepositsProcessor = Substitute.For<IMaturedBlockDepositsProcessor>();
            this.chain = Substitute.ForPartsOf<ConcurrentChain>();
        }

        [Fact]
        public void ExtractMaturedBlockDeposits_Returns_MaturedBlockDepositsModel()
        {            
            (ChainedHeaderBlock chainedHeaderBlock, Block block) = ChainHeaderBlockBuilder();

            IReadOnlyList<IDeposit> deposits = Substitute.For<IReadOnlyList<IDeposit>>();
            var maturedBlockDepositsModel = new MaturedBlockDepositsModel(new MaturedBlockModel(), deposits);

            this.maturedBlockDepositsProcessor.ExtractMaturedBlockDeposits(chainedHeaderBlock).Returns(maturedBlockDepositsModel);
        }

        private (ChainedHeaderBlock chainedHeaderBlock, Block block) ChainHeaderBlockBuilder()
        {            
            var blockHeader = new BlockHeader();
            var chainedHeader = new ChainedHeader(blockHeader, uint256.Zero, 1);
            this.chain.GetBlock(0).Returns(chainedHeader);

            var block = new Block();
            var chainedHeaderBlock = new ChainedHeaderBlock(block, chainedHeader);

            return (chainedHeaderBlock, block);
        }
    }
}
