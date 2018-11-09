using System.Collections.Generic;
using NBitcoin;
using NSubstitute;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Primitives;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Xunit;
using Stratis.FederatedPeg.Features.FederationGateway.Models;
using BlockObserver = Stratis.FederatedPeg.Features.FederationGateway.Notifications.BlockObserver;

namespace Stratis.FederatedPeg.Tests
{
    public class BlockObserverTests
    {
        private BlockObserver blockObserver;

        private readonly IFederationWalletSyncManager federationWalletSyncManager;

        private readonly ICrossChainTransactionMonitor crossChainTransactionMonitor;

        private readonly ILeaderProvider leaderProvider;

        private readonly IFederationGatewaySettings federationGatewaySettings;

        private readonly IFullNode fullNode;

        private readonly ConcurrentChain chain;

        private readonly uint minimumDepositConfirmations;

        private IMaturedBlockSender maturedBlockSender;

        private IBlockTipSender blockTipSender;

        private IMaturedBlockDepositsProcessor maturedBlockDepositsProcessor;

        public BlockObserverTests()
        {
            this.federationGatewaySettings = Substitute.For<IFederationGatewaySettings>();
            this.federationGatewaySettings.MinimumDepositConfirmations.Returns(this.minimumDepositConfirmations);

            this.crossChainTransactionMonitor = Substitute.For<ICrossChainTransactionMonitor>();
            this.federationWalletSyncManager = Substitute.For<IFederationWalletSyncManager>();
            this.fullNode = Substitute.For<IFullNode>();
            this.maturedBlockSender = Substitute.For<IMaturedBlockSender>();
            this.blockTipSender = Substitute.For<IBlockTipSender>();
            this.maturedBlockDepositsProcessor = Substitute.For<IMaturedBlockDepositsProcessor>();
            this.chain = Substitute.ForPartsOf<ConcurrentChain>();
            this.fullNode.NodeService<ConcurrentChain>().Returns(this.chain);

            this.blockObserver = new BlockObserver(
                this.federationWalletSyncManager,
                this.crossChainTransactionMonitor,
                this.fullNode,
                this.maturedBlockSender,
                this.blockTipSender,
                this.maturedBlockDepositsProcessor);
        }

        [Fact]
        public void BlockObserver_Should_Not_Try_To_Extract_Deposits_Before_MinimumDepositConfirmations()
        {
            var confirmations = (int)this.minimumDepositConfirmations - 1;

            var earlyBlock = new Block();
            var earlyChainHeaderBlock = new ChainedHeaderBlock(earlyBlock, new ChainedHeader(new BlockHeader(), uint256.Zero, confirmations));


            this.blockObserver.OnNext(earlyChainHeaderBlock);

            this.crossChainTransactionMonitor.Received(1).ProcessBlock(earlyBlock);
            this.federationWalletSyncManager.Received(1).ProcessBlock(earlyBlock);
            this.maturedBlockDepositsProcessor.Received(0).ExtractMaturedBlockDeposits(null);
            this.maturedBlockSender.ReceivedWithAnyArgs(0).SendMaturedBlockDepositsAsync(null);
        }

        [Fact]
        public void BlockObserver_Should_Try_To_Extract_Deposits_After_MinimumDepositConfirmations()
        {
            var blockBuilder = this.ChainHeaderBlockBuilder();

            IReadOnlyList<IDeposit> deposits = Substitute.For<IReadOnlyList<IDeposit>>();
            var maturedBlockDepositsModel = new MaturedBlockDepositsModel(new MaturedBlockModel(), deposits);
            this.maturedBlockDepositsProcessor.ExtractMaturedBlockDeposits(blockBuilder.chainedHeaderBlock).Returns(maturedBlockDepositsModel);

            this.blockObserver.OnNext(blockBuilder.chainedHeaderBlock);

            this.crossChainTransactionMonitor.Received(1).ProcessBlock(blockBuilder.block);
            this.federationWalletSyncManager.Received(1).ProcessBlock(blockBuilder.block);
            this.maturedBlockDepositsProcessor.Received(1).ExtractMaturedBlockDeposits(blockBuilder.chainedHeaderBlock);
            this.maturedBlockSender.ReceivedWithAnyArgs(1).SendMaturedBlockDepositsAsync(null);
        }

        [Fact]
        public void BlockObserver_Should_Send_Block_Tip()
        {
            ChainedHeaderBlock chainedHeaderBlock = this.ChainHeaderBlockBuilder().chainedHeaderBlock;

            this.blockObserver.OnNext(chainedHeaderBlock);

            this.blockTipSender.ReceivedWithAnyArgs(1).SendBlockTipAsync(null);
        }

        private(ChainedHeaderBlock chainedHeaderBlock, Block block) ChainHeaderBlockBuilder()
        {
            var confirmations = (int)this.minimumDepositConfirmations;

            var blockHeader = new BlockHeader();
            var chainedHeader = new ChainedHeader(blockHeader, uint256.Zero, confirmations);
            this.chain.GetBlock(0).Returns(chainedHeader);

            var block = new Block();
            var chainedHeaderBlock = new ChainedHeaderBlock(block, chainedHeader);

            return (chainedHeaderBlock, block);
        }
    }
}
