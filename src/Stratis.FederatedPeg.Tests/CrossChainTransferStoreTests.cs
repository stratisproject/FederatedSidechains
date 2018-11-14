﻿using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NSubstitute;
using Stratis.Bitcoin.Configuration;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.TargetChain;
using Xunit;
using Stratis.Sidechains.Networks;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Features.BlockStore;

namespace Stratis.FederatedPeg.Tests
{
    public class CrossChainTransferStoreTests
    {
        private readonly Network network;
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger logger;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly IDepositExtractor depositExtractor;
        private readonly IBlockRepository blockRepository;
        private readonly IFullNode fullNode;
        private readonly IFederationWalletManager federationWalletManager;
        private readonly IFederationGatewaySettings federationGatewaySettings;
        private Dictionary<uint256, Block> blockDict;
        
        /// <summary>
        /// Initializes the cross-chain transfer tests.
        /// </summary>
        public CrossChainTransferStoreTests()
        {
            this.network = ApexNetwork.RegTest;

            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.logger = Substitute.For<ILogger>();
            this.loggerFactory.CreateLogger(null).ReturnsForAnyArgs(this.logger);
            this.dateTimeProvider = DateTimeProvider.Default;
            this.depositExtractor = Substitute.For<IDepositExtractor>();
            this.blockRepository = Substitute.For<IBlockRepository>();
            this.fullNode = Substitute.For<IFullNode>();
            this.federationWalletManager = Substitute.For<IFederationWalletManager>();
            this.federationGatewaySettings = Substitute.For<IFederationGatewaySettings>();
            var redeemScript = new Script("2 026ebcbf6bfe7ce1d957adbef8ab2b66c788656f35896a170257d6838bda70b95c 02a97b7d0fad7ea10f456311dcd496ae9293952d4c5f2ebdfc32624195fde14687 02e9d3cd0c2fa501957149ff9d21150f3901e6ece0e3fe3007f2372720c84e3ee1 03c99f997ed71c7f92cf532175cea933f2f11bf08f1521d25eb3cc9b8729af8bf4 034b191e3b3107b71d1373e840c5bf23098b55a355ca959b968993f5dec699fc38 5 OP_CHECKMULTISIG");
            this.federationGatewaySettings.IsMainChain.Returns(false);
            this.federationGatewaySettings.MultiSigRedeemScript.Returns(redeemScript);
            this.federationGatewaySettings.MultiSigAddress.Returns(redeemScript.Hash.GetAddress(this.network));
            this.federationGatewaySettings.PublicKey.Returns("026ebcbf6bfe7ce1d957adbef8ab2b66c788656f35896a170257d6838bda70b95c");

            this.blockDict = new Dictionary<uint256, Block>();

            this.blockRepository.GetBlocksAsync(Arg.Any<List<uint256>>()).ReturnsForAnyArgs((x) => {
                List<uint256> hashes = x.ArgAt<List<uint256>>(0);
                var blocks = new List<Block>();
                for (int i = 0; i < hashes.Count; i++)
                {
                    blocks.Add(this.blockDict.TryGetValue(hashes[i], out Block block) ? block : null);
                }

                return blocks;
            });
        }


        /// <summary>
        /// Test that after synchronizing with the chain the store tip equals the chain tip.
        /// </summary>
        [Fact]
        public void SynchronizeSynchronizesWithChain()
        {
            ConcurrentChain chain = BuildChain(5);
            var dataFolder = new DataFolder(CreateTestDir(this));

            using (var crossChainTransferStore = new CrossChainTransferStore(this.network, dataFolder, chain, this.federationGatewaySettings,
                this.dateTimeProvider, this.loggerFactory, this.depositExtractor, this.fullNode, this.blockRepository, this.federationWalletManager))
            {
                crossChainTransferStore.Initialize();

                Assert.True(crossChainTransferStore.SynchronizeAsync().GetAwaiter().GetResult());
                Assert.Equal(chain.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.Hash);
                Assert.Equal(chain.Tip.Height, crossChainTransferStore.TipHashAndHeight.Height);
            }
        }

        /// <summary>
        /// Test that after synchronizing with the chain the store tip equals the chain tip.
        /// </summary>
        [Fact]
        public void SynchronizeSynchronizesWithChainAndSurvivesRestart()
        {
            ConcurrentChain chain = BuildChain(5);
            var dataFolder = new DataFolder(CreateTestDir(this));

            using (var crossChainTransferStore = new CrossChainTransferStore(this.network, dataFolder, chain, this.federationGatewaySettings,
                this.dateTimeProvider, this.loggerFactory, this.depositExtractor, this.fullNode, this.blockRepository, this.federationWalletManager))
            {
                crossChainTransferStore.Initialize();

                Assert.True(crossChainTransferStore.SynchronizeAsync().GetAwaiter().GetResult());
                Assert.Equal(chain.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.Hash);
                Assert.Equal(chain.Tip.Height, crossChainTransferStore.TipHashAndHeight.Height);
            }

            // Create a new instance of this test that loads from the persistence that we created in the step before.
            var newTest = new CrossChainTransferStoreTests();
            ConcurrentChain newChain = newTest.BuildChain(3);

            using (var crossChainTransferStore2 = new CrossChainTransferStore(newTest.network, dataFolder, newChain, this.federationGatewaySettings,
                newTest.dateTimeProvider, newTest.loggerFactory, newTest.depositExtractor, newTest.fullNode, newTest.blockRepository, this.federationWalletManager))
            {
                crossChainTransferStore2.Initialize();

                // Test that the store was reloaded from persistence.
                Assert.Equal(chain.Tip.HashBlock, crossChainTransferStore2.TipHashAndHeight.Hash);
                Assert.Equal(chain.Tip.Height, crossChainTransferStore2.TipHashAndHeight.Height);

                // Test that synchronizing the store aligns it with the current chain tip.
                Assert.True(crossChainTransferStore2.SynchronizeAsync().GetAwaiter().GetResult());
                Assert.Equal(newChain.Tip.HashBlock, crossChainTransferStore2.TipHashAndHeight.Hash);
                Assert.Equal(newChain.Tip.Height, crossChainTransferStore2.TipHashAndHeight.Height);
            }
        }

        /// <summary>
        /// Builds a chain with the requested number of blocks.
        /// </summary>
        /// <param name="blocks">The number of blocks.</param>
        /// <returns>A chain with the requested number of blocks.</returns>
        private ConcurrentChain BuildChain(int blocks)
        {
            ConcurrentChain chain = new ConcurrentChain(this.network);

            this.blockDict.Clear();
            this.blockDict[this.network.GenesisHash] = this.network.GetGenesis();

            for (int i = 0; i < blocks - 1; i++)
            {
                this.AppendBlock(chain);
            }

            return chain;
        }

        /// <summary>
        /// Create a block and add it to the dictionary used by the mock block repository.
        /// </summary>
        /// <param name="previous">Previous chained header.</param>
        /// <param name="chains">Chains to add the block to.</param>
        /// <returns>The last chained header.</returns>
        private ChainedHeader AppendBlock(ChainedHeader previous, params ConcurrentChain[] chains)
        {
            ChainedHeader last = null;
            uint nonce = RandomUtils.GetUInt32();
            foreach (ConcurrentChain chain in chains)
            {
                Block block = this.network.CreateBlock();
                block.AddTransaction(this.network.CreateTransaction());
                block.UpdateMerkleRoot();
                block.Header.HashPrevBlock = previous == null ? chain.Tip.HashBlock : previous.HashBlock;
                block.Header.Nonce = nonce;
                if (!chain.TrySetTip(block.Header, out last))
                    throw new InvalidOperationException("Previous not existing");
                this.blockDict[block.GetHash()] = block;
            }
            return last;
        }

        /// <summary>
        /// Append a block to the specified chain(s).
        /// </summary>
        /// <param name="chains">The chains to append a block to.</param>
        /// <returns>The last chained header.</returns>
        private ChainedHeader AppendBlock(params ConcurrentChain[] chains)
        {
            ChainedHeader index = null;
            return this.AppendBlock(index, chains);
        }

        /// <summary>
        /// Creates a directory for a test, based on the name of the class containing the test and the name of the test.
        /// </summary>
        /// <param name="caller">The calling object, from which we derive the namespace in which the test is contained.</param>
        /// <param name="callingMethod">The name of the test being executed. A directory with the same name will be created.</param>
        /// <returns>The path of the directory that was created.</returns>
        public static string CreateTestDir(object caller, [System.Runtime.CompilerServices.CallerMemberName] string callingMethod = "")
        {
            string directoryPath = GetTestDirectoryPath(caller, callingMethod);
            return AssureEmptyDir(directoryPath);
        }

        /// <summary>
        /// Gets the path of the directory that <see cref="CreateTestDir(object, string)"/> or <see cref="CreateDataFolder(object, string)"/> would create.
        /// </summary>
        /// <remarks>The path of the directory is of the form TestCase/{testClass}/{testName}.</remarks>
        /// <param name="caller">The calling object, from which we derive the namespace in which the test is contained.</param>
        /// <param name="callingMethod">The name of the test being executed. A directory with the same name will be created.</param>
        /// <returns>The path of the directory.</returns>
        public static string GetTestDirectoryPath(object caller, [System.Runtime.CompilerServices.CallerMemberName] string callingMethod = "")
        {
            return GetTestDirectoryPath(Path.Combine(caller.GetType().Name, callingMethod));
        }

        /// <summary>
        /// Gets the path of the directory that <see cref="CreateTestDir(object, string)"/> would create.
        /// </summary>
        /// <remarks>The path of the directory is of the form TestCase/{testClass}/{testName}.</remarks>
        /// <param name="testDirectory">The directory in which the test files are contained.</param>
        /// <returns>The path of the directory.</returns>
        public static string GetTestDirectoryPath(string testDirectory)
        {
            return Path.Combine("..", "..", "..", "..", "TestCase", testDirectory);
        }

        public static string AssureEmptyDir(string dir)
        {
            string uniqueDirName = $"{dir}-{DateTime.UtcNow:ddMMyyyyTHH.mm.ss.fff}";
            Directory.CreateDirectory(uniqueDirName);
            return uniqueDirName;
        }
    }
}
