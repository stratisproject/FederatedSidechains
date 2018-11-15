﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DBreeze;
using DBreeze.DataTypes;
using DBreeze.Utils;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.SourceChain;
using Recipient = Stratis.FederatedPeg.Features.FederationGateway.Wallet;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    /// <summary>
    /// Interface for interacting with the cross-chain transfer database.
    /// </summary>
    public interface ICrossChainTransferStore : IDisposable
    {
        /// <summary>
        /// Initializes the cross-chain-transfer store.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Starts the cross-chain-transfer store.
        /// </summary>
        void Start();

        /// <summary>
        /// Records the mature deposits from <see cref="NextMatureDepositHeight"/> on the counter-chain.
        /// The value of <see cref="NextMatureDepositHeight"/> is incremented at the end of this call.
        /// </summary>
        /// <param name="deposits">The deposits.</param>
        /// <remarks>
        /// The transfers are set to <see cref="CrossChainTransfer.Status"/> of <see cref="CrossChainTransferStatus.Partial"/>
        /// or <see cref="CrossChainTransferStatus.Rejected"/> depending on whether enough funds are available in the federation wallet.
        /// </remarks>
        Task RecordLatestMatureDepositsAsync(IDeposit[] deposits);

        /// <summary>
        /// Returns all partial transactions still in need of signatures.
        /// </summary>
        /// <returns>An array of fully signed transactions.</returns>
        Task<Transaction[]> GetPartialTransactionsAsync();

        /// <summary>
        /// Updates partial transactions in the store with signatures obtained from the passed transactions.
        /// The <see cref="CrossChainTransferStatus.FullySigned"/> status is set on fully signed transactions.
        /// </summary>
        /// <param name="depositId">The deposit transaction to update.</param>
        /// <param name="partialTransactions">Partial transactions received from other federation members.</param>
        Task MergeTransactionSignaturesAsync(uint256 depositId, Transaction[] partialTransactions);

        /// <summary>
        /// Returns all fully signed transactions ready to broadcast. The caller is responsible for checking the memory pool and
        /// not re-broadcasting transactions unneccessarily.
        /// </summary>
        /// <returns>An array of fully signed transactions.</returns>
        Task<Transaction[]> GetSignedTransactionsAsync();

        /// <summary>
        /// Get the cross-chain transfer information from the database, identified by the deposit transaction ids.
        /// </summary>
        /// <param name="depositIds">The deposit transaction ids.</param>
        /// <returns>The cross-chain transfer information.</returns>
        Task<CrossChainTransfer[]> GetAsync(uint256[] depositIds);

        /// <summary>
        /// The tip of our chain when we last updated the store.
        /// </summary>
        HashHeightPair TipHashAndHeight { get; }

        /// <summary>
        /// The block height on the counter-chain for which the next list of deposits is expected.
        /// </summary>
        int NextMatureDepositHeight { get; }
    }

    public class CrossChainTransferStore : ICrossChainTransferStore
    {
        /// <summary>This table contains the cross-chain transfer information.</summary>
        private const string transferTableName = "Transfers";

        /// <summary>This table keeps track of the chain tips so that we know exactly what data our transfer table contains.</summary>
        private const string commonTableName = "Common";

        // <summary>Block batch size for synchronization</summary>
        private const int synchronizationBatchSize = 100;

        /// <summary>This contains deposits ids indexed by block hash of the corresponding transaction.</summary>
        private Dictionary<uint256, HashSet<uint256>> depositIdsByBlockHash = new Dictionary<uint256, HashSet<uint256>>();

        /// <summary>This contains the block heights by block hashes for only the blocks of interest in our chain.</summary>
        private Dictionary<uint256, int> blockHeightsByBlockHash = new Dictionary<uint256, int>();

        /// <summary>This table contains deposits ids by status.</summary>
        private Dictionary<CrossChainTransferStatus, HashSet<uint256>> depositsIdsByStatus = new Dictionary<CrossChainTransferStatus, HashSet<uint256>>();

        /// <inheritdoc />
        public int NextMatureDepositHeight { get; private set; }

        /// <inheritdoc />
        public HashHeightPair TipHashAndHeight { get; private set; }

        /// <summary>The key of the repository tip in the common table.</summary>
        private static readonly byte[] RepositoryTipKey = new byte[] { 0 };

        /// <summary>The key of the counter-chain last mature block tip in the common table.</summary>
        private static readonly byte[] NextMatureTipKey = new byte[] { 1 };

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Access to DBreeze database.</summary>
        private readonly DBreezeEngine DBreeze;

        private readonly Network network;

        private readonly ConcurrentChain chain;

        private readonly DepositExtractor depositExtractor;

        private readonly IBlockRepository blockRepository;

        private readonly CancellationTokenSource cancellation;

        private readonly IFederationWalletManager federationWalletManager;

        private readonly IFederationWalletTransactionHandler federationWalletTransactionHandler;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        public CrossChainTransferStore(Network network, DataFolder dataFolder, ConcurrentChain chain, IFederationGatewaySettings settings, IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory, IOpReturnDataReader opReturnDataReader, IFullNode fullNode, IBlockRepository blockRepository,
            IFederationWalletManager federationWalletManager, IFederationWalletTransactionHandler federationWalletTransactionHandler)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(dataFolder, nameof(dataFolder));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(settings, nameof(settings));
            Guard.NotNull(dateTimeProvider, nameof(dateTimeProvider));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(opReturnDataReader, nameof(opReturnDataReader));
            Guard.NotNull(fullNode, nameof(fullNode));
            Guard.NotNull(blockRepository, nameof(blockRepository));
            Guard.NotNull(federationWalletManager, nameof(federationWalletManager));
            Guard.NotNull(federationWalletTransactionHandler, nameof(federationWalletTransactionHandler));

            this.network = network;
            this.chain = chain;
            this.dateTimeProvider = dateTimeProvider;
            this.blockRepository = blockRepository;
            this.federationWalletManager = federationWalletManager;
            this.federationWalletTransactionHandler = federationWalletTransactionHandler;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            string folder = Path.Combine(dataFolder.RootPath, settings.IsMainChain ? "mainchaindata" : "sidechaindata");
            Directory.CreateDirectory(folder);
            this.DBreeze = new DBreezeEngine(folder);

            this.depositExtractor = new DepositExtractor(loggerFactory, settings, opReturnDataReader, fullNode);
            this.TipHashAndHeight = null;
            this.NextMatureDepositHeight = 0;
            this.cancellation = new CancellationTokenSource();

            // Initialize tracking deposits by status.
            foreach (var status in typeof(CrossChainTransferStatus).GetEnumValues())
                this.depositsIdsByStatus[(CrossChainTransferStatus)status] = new HashSet<uint256>();
        }

        /// <summary>Performs any needed initialisation for the database.</summary>
        public void Initialize()
        {
            this.logger.LogTrace("()");

            using (DBreeze.Transactions.Transaction dbreezeTransaction = this.DBreeze.GetTransaction())
            {
                this.LoadTipHashAndHeight(dbreezeTransaction);
                this.LoadNextMatureHeight(dbreezeTransaction);

                // Initialize the lookups.
                foreach (Row<byte[], CrossChainTransfer> transferRow in dbreezeTransaction.SelectForward<byte[], CrossChainTransfer>(transferTableName))
                {
                    CrossChainTransfer transfer = transferRow.Value;

                    this.depositsIdsByStatus[transfer.Status].Add(transfer.DepositTransactionId);

                    if (transfer.BlockHash != null)
                    {
                        if (!this.depositIdsByBlockHash.TryGetValue(transfer.BlockHash, out HashSet<uint256> deposits))
                        {
                            deposits = new HashSet<uint256>();
                        }

                        deposits.Add(transfer.DepositTransactionId);

                        this.blockHeightsByBlockHash[transfer.BlockHash] = transfer.BlockHeight;
                    }
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Starts the cross-chain-transfer store.
        /// </summary>
        public void Start()
        {
            this.SynchronizeAsync().GetAwaiter().GetResult();
            this.SanityCheckAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Partial or fully signed transfers should have their source UTXO's recorded by an up-to-date wallet.
        /// Sets transfers to <see cref="CrossChainTransferStatus.Rejected"/> if their UTXO's are not reserved
        /// within the wallet.
        /// </summary>
        public async Task SanityCheckAsync()
        {
            this.logger.LogTrace("()");

            Recipient.FederationWallet wallet = this.federationWalletManager.GetWallet();

            // Can only do a sanity check on an up-to-date wallet.
            if (this.chain.Tip.HashBlock != wallet.LastBlockSyncedHash)
                return;

            // Ensure that the store is up-to-date as well.
            await this.SynchronizeAsync();

            using (DBreeze.Transactions.Transaction dbreezeTransaction = this.DBreeze.GetTransaction())
            {
                dbreezeTransaction.SynchronizeTables(transferTableName, commonTableName);

                CrossChainTransfer[] partialTransfers = this.Get(dbreezeTransaction,
                this.depositsIdsByStatus[CrossChainTransferStatus.Partial].Union(
                    this.depositsIdsByStatus[CrossChainTransferStatus.FullySigned]).ToArray());

                foreach (CrossChainTransfer partialTransfer in partialTransfers)
                {
                    if (!SanityCheck(partialTransfer.PartialTransaction, wallet))
                    {
                        this.SetTransferStatus(partialTransfer, CrossChainTransferStatus.Rejected);
                        await this.PutTransferAsync(dbreezeTransaction, partialTransfer);
                    }
                }

                dbreezeTransaction.Commit();
            }
        }

        /// <inheritdoc />
        public async Task RecordLatestMatureDepositsAsync(IDeposit[] deposits)
        {
            Guard.NotNull(deposits, nameof(deposits));
            Guard.Assert(!deposits.Any(d => d.BlockNumber != this.NextMatureDepositHeight));

            Recipient.FederationWallet wallet = this.federationWalletManager.GetWallet();

            Guard.Assert(wallet.LastBlockSyncedHash == this.chain.Tip.HashBlock);

            this.logger.LogTrace("()");

            using (DBreeze.Transactions.Transaction dbreezeTransaction = this.DBreeze.GetTransaction())
            {
                dbreezeTransaction.SynchronizeTables(transferTableName, commonTableName);

                // Check if the deposits already exist which could happen if it was found on the chain.
                CrossChainTransfer[] transfers = this.Get(dbreezeTransaction, deposits.Select(d => d.Id).ToArray());

                for (int i = 0; i < deposits.Length; i++)
                {
                    IDeposit deposit = deposits[i];
                    CrossChainTransfer transfer = transfers[i];

                    Script scriptPubKey = BitcoinAddress.Create(deposit.TargetAddress, this.network).ScriptPubKey;

                    var recipient = new List<Recipient.Recipient>()
                    {
                        new Recipient.Recipient
                        {
                            Amount = deposit.Amount,
                            ScriptPubKey = scriptPubKey
                        }
                    };

                    Transaction transaction = null;

                    try
                    {
                        string opReturnData = deposit.BlockNumber.ToString();

                        // Build the multisig transaction template.
                        var multiSigContext = new Wallet.TransactionBuildContext(recipient, opReturnData: Encoding.UTF8.GetBytes(opReturnData))
                        {
                            TransactionFee = Money.Coins(0.01m), // TODO
                            MinConfirmations = 0,                // TODO
                            Shuffle = false,
                            MultiSig = wallet.MultiSigAddress,
                            IgnoreVerify = true,
                            Sign = false
                        };

                        // Build the transaction.
                        transaction = this.federationWalletTransactionHandler.BuildTransaction(multiSigContext);

                        // TODO: Include my signature?
                    }
                    catch (Exception)
                    {
                    }

                    if (transfer == null)
                    {
                        transfer = new CrossChainTransfer((transaction != null) ? CrossChainTransferStatus.Partial : CrossChainTransferStatus.Rejected,
                            deposit.Id, deposit.BlockNumber, scriptPubKey, deposit.Amount, transaction, 0, -1 /* Unknown */);

                        this.depositsIdsByStatus[transfer.Status].Add(transfer.DepositTransactionId);
                    }
                    else
                    {
                        transfer = new CrossChainTransfer(transfer.Status, deposit.Id, deposit.BlockNumber, scriptPubKey, deposit.Amount, transfer.PartialTransaction,
                            transfer.BlockHash, transfer.BlockHeight);
                    }

                    await this.PutTransferAsync(dbreezeTransaction, transfer);

                    if (transaction != null)
                    {
                        this.federationWalletManager.ProcessTransaction(transaction);
                    }
                }

                // Commit additions
                this.SaveNextMatureHeight(dbreezeTransaction, this.NextMatureDepositHeight + 1);
                dbreezeTransaction.Commit();
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public async Task MergeTransactionSignaturesAsync(uint256 depositId, Transaction[] partialTransactions)
        {
            Guard.NotNull(partialTransactions, nameof(partialTransactions));

            this.logger.LogTrace("()");

            using (DBreeze.Transactions.Transaction dbreezeTransaction = this.DBreeze.GetTransaction())
            {
                dbreezeTransaction.SynchronizeTables(transferTableName, commonTableName);

                CrossChainTransfer transfer = this.Get(dbreezeTransaction, new[] { depositId }).FirstOrDefault();

                if (transfer != null)
                {
                    transfer.CombineSignatures(this.network, partialTransactions);

                    // TODO: Update status to FullySigned when appropriate.

                    await this.PutTransferAsync(dbreezeTransaction, transfer);
                }

                dbreezeTransaction.Commit();
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Uses the information contained in our chain's blocks to update the store.
        /// Sets the <see cref="CrossChainTransferStatus.SeenInBlock"/> status for transfers
        /// identified in the blocks.
        /// </summary>
        /// <param name="newTip">The new <see cref="ChainTip"/>.</param>
        /// <param name="blocks">The blocks used to update the store. Must be sorted by ascending height leading up to the new tip.</param>
        private async Task PutAsync(HashHeightPair newTip, List<Block> blocks)
        {
            Guard.NotNull(newTip, nameof(newTip));
            Guard.NotNull(blocks, nameof(blocks));
            Guard.Assert(blocks.Count == 0 || blocks[0].Header.HashPrevBlock == (this.TipHashAndHeight?.Hash ?? 0));

            this.logger.LogTrace("()");

            using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
            {
                transaction.SynchronizeTables(transferTableName, commonTableName);
                await this.OnInsertBlocksAsync(transaction, newTip.Height - blocks.Count + 1, blocks);

                // Commit additions
                this.SaveTipHashAndHeight(transaction, newTip);
                transaction.Commit();
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Used to handle reorg (if required) and revert status from <see cref="CrossChainTransferStatus.SeenInBlock"/> to
        /// <see cref="CrossChainTransferStatus.FullySigned"/>. Also returns a flag to indicate whether we are behind the current tip.
        /// The caller can use <see cref="PutAsync"/> to supply additional blocks if we are behind the tip.
        /// </summary>
        /// <returns>
        /// Returns <c>true</c> if we match the chain tip and <c>false</c> if we are behind the tip.
        /// </returns>
        private async Task<bool> RewindIfRequiredAsync()
        {
            this.logger.LogTrace("()");

            if (this.chain.Tip.HashBlock == (this.TipHashAndHeight?.Hash ?? 0))
            {
                // Indicate that we are synchronized.
                this.logger.LogTrace("(-):true");
                return true;
            }

            // If the chain does not contain our tip..
            if (this.TipHashAndHeight != null && this.chain.GetBlock(this.TipHashAndHeight.Hash) == null)
            {
                // We are ahead of the current chain or on the wrong chain.
                uint256 commonTip = this.network.GenesisHash;
                int commonHeight = 0;

                ChainedHeader fork = this.chain.FindFork(this.depositIdsByBlockHash.OrderByDescending(d => this.blockHeightsByBlockHash[d.Key]).Select(d => d.Key));

                if (fork != null)
                {
                    commonTip = fork.Block.GetHash();
                    commonHeight = this.blockHeightsByBlockHash[commonTip];
                }

                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    transaction.SynchronizeTables(transferTableName, commonTableName);
                    transaction.ValuesLazyLoadingIsOn = false;
                    await this.OnDeleteBlocksAsync(transaction, commonHeight);
                    this.SaveTipHashAndHeight(transaction, new HashHeightPair(commonTip, commonHeight));
                    transaction.Commit();
                }

                bool caughtUp = commonTip == this.chain.Tip.HashBlock;

                // Indicate that we have rewound to the current chain.
                this.logger.LogTrace("(-):{0}", caughtUp);
                return caughtUp;
            }

            // Indicate that we are behind the current chain.
            this.logger.LogTrace("(-):false");
            return false;
        }

        /// <summary>
        /// Attempts to synchronizes the store with the chain.
        /// </summary>
        /// <returns>
        /// Returns <c>true</c> if the store is in sync or <c>false</c> otherwise.
        /// </returns>
        private async Task<bool> SynchronizeAsync()
        {
            this.logger.LogTrace("()");

            while (!this.cancellation.IsCancellationRequested)
            {
                if (await this.RewindIfRequiredAsync())
                {
                    this.logger.LogTrace("(-):true");
                    return true;
                }

                if (!await SynchronizeBatchAsync())
                {
                    this.logger.LogTrace("(-):false");
                    return false;
                }
            }

            this.logger.LogTrace("(-):false");
            return false;
        }

        /// <summary>
        /// Synchronize with a batch of blocks.
        /// </summary>
        /// <returns>Returns <c>true</c> if we match the chain tip and <c>false</c> if we are behind the tip.</returns>
        private async Task<bool> SynchronizeBatchAsync()
        {
            this.logger.LogTrace("()");

            // Get a batch of blocks.
            var blockHashes = new List<uint256>();
            int batchSize = 0;

            foreach (ChainedHeader header in this.chain.EnumerateToTip(this.TipHashAndHeight?.Hash ?? this.network.GenesisHash).Skip(this.TipHashAndHeight == null ? 0 : 1))
            {
                blockHashes.Add(header.HashBlock);
                if (++batchSize >= synchronizationBatchSize)
                    break;
            }

            List<Block> blocks = await this.blockRepository.GetBlocksAsync(blockHashes);
            int availableBlocks = blocks.FindIndex(b => (b == null));
            if (availableBlocks < 0)
                availableBlocks = blocks.Count;

            if (availableBlocks > 0)
            {
                Block lastBlock = blocks[availableBlocks - 1];
                HashHeightPair newTip = new HashHeightPair(lastBlock.GetHash(), (this.TipHashAndHeight?.Height ?? -1) + availableBlocks);

                await this.PutAsync(newTip, blocks.GetRange(0, availableBlocks));
            }

            this.logger.LogTrace("Synchronized {0} blocks with cross-chain store.", availableBlocks);

            bool success = availableBlocks == blocks.Count;

            this.logger.LogTrace("(-):{0}", success);
            return success;
        }

        /// <summary>
        /// Loads the tip and hash height.
        /// </summary>
        /// <param name="dbreezeTransaction">The DBreeze transaction context to use.</param>
        /// <returns>The hash and height pair.</returns>
        private HashHeightPair LoadTipHashAndHeight(DBreeze.Transactions.Transaction dbreezeTransaction)
        {
            if (this.TipHashAndHeight == null)
            {
                dbreezeTransaction.ValuesLazyLoadingIsOn = false;

                Row<byte[], byte[]> row = dbreezeTransaction.Select<byte[], byte[]>(commonTableName, RepositoryTipKey);
                if (row.Exists)
                    this.TipHashAndHeight = HashHeightPair.Load(row.Value);
            }

            return this.TipHashAndHeight;
        }

        /// <summary>
        /// Saves the tip and hash height.
        /// </summary>
        /// <param name="dbreezeTransaction">The DBreeze transaction context to use.</param>
        /// <param name="newTip">The new tip to persist.</param>
        private void SaveTipHashAndHeight(DBreeze.Transactions.Transaction dbreezeTransaction, HashHeightPair newTip)
        {
            this.TipHashAndHeight = newTip;
            dbreezeTransaction.Insert<byte[], byte[]>(commonTableName, RepositoryTipKey, this.TipHashAndHeight.ToBytes());
        }

        /// <summary>
        /// Loads the counter-chain next mature block height.
        /// </summary>
        /// <param name="dbreezeTransaction">The DBreeze transaction context to use.</param>
        /// <returns>The hash and height pair.</returns>
        private int LoadNextMatureHeight(DBreeze.Transactions.Transaction dbreezeTransaction)
        {
            if (this.TipHashAndHeight == null)
            {
                dbreezeTransaction.ValuesLazyLoadingIsOn = false;

                Row<byte[], int> row = dbreezeTransaction.Select<byte[], int>(commonTableName, NextMatureTipKey);
                if (row.Exists)
                    this.NextMatureDepositHeight = row.Value;
            }

            return this.NextMatureDepositHeight;
        }

        /// <summary>
        /// Saves the counter-chain next mature block height.
        /// </summary>
        /// <param name="dbreezeTransaction">The DBreeze transaction context to use.</param>
        /// <param name="newTip">The next mature block height on the counter-chain.</param>
        private void SaveNextMatureHeight(DBreeze.Transactions.Transaction dbreezeTransaction, int newTip)
        {
            this.NextMatureDepositHeight = newTip;
            dbreezeTransaction.Insert<byte[], int>(commonTableName, NextMatureTipKey, this.NextMatureDepositHeight);
        }

        /// <inheritdoc />
        public async Task<CrossChainTransfer[]> GetAsync(uint256[] depositId)
        {
            this.logger.LogTrace("()");
            using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
            {
                transaction.ValuesLazyLoadingIsOn = false;

                return Get(transaction, depositId);
            }
        }

        private CrossChainTransfer[] Get(DBreeze.Transactions.Transaction transaction, uint256[] depositId)
        {
            Guard.NotNull(depositId, nameof(depositId));

            // To boost performance we will access the deposits sorted by deposit id.
            var depositDict = new Dictionary<uint256, int>();
            for (int i = 0; i < depositId.Length; i++)
                depositDict[depositId[i]] = i;

            var byteListComparer = new ByteListComparer();
            List<KeyValuePair<uint256, int>> depositList = depositDict.ToList();
            depositList.Sort((pair1, pair2) => byteListComparer.Compare(pair1.Key.ToBytes(), pair2.Key.ToBytes()));

            var res = new CrossChainTransfer[depositId.Length];

            foreach (KeyValuePair<uint256, int> kv in depositList)
            {
                Row<byte[], CrossChainTransfer> transferRow = transaction.Select<byte[], CrossChainTransfer>(transferTableName, kv.Key.ToBytes());

                if (transferRow.Exists)
                {
                    res[kv.Value] = transferRow.Value;
                }
            }

            return res;
        }

        /// <inheritdoc />
        public async Task<Transaction[]> GetSignedTransactionsAsync()
        {
            this.logger.LogTrace("()");

            await this.SynchronizeAsync();
            await this.SanityCheckAsync();

            uint256[] signedTransferHashes = this.depositsIdsByStatus[CrossChainTransferStatus.FullySigned].ToArray();

            CrossChainTransfer[] signedTransfers = await this.GetAsync(signedTransferHashes);

            Transaction[] res = signedTransfers.Select(t => t.PartialTransaction).ToArray();

            this.logger.LogTrace("(-){0}", res);

            return res;
        }

        /// <inheritdoc />
        public async Task<Transaction[]> GetPartialTransactionsAsync()
        {
            this.logger.LogTrace("()");

            await this.SynchronizeAsync();
            await this.SanityCheckAsync();

            uint256[] partialTransferHashes = this.depositsIdsByStatus[CrossChainTransferStatus.Partial].ToArray();

            CrossChainTransfer[] partialTransfers = await this.GetAsync(partialTransferHashes);

            Transaction[] res = partialTransfers.Select(t => t.PartialTransaction).ToArray();

            this.logger.LogTrace("(-){0}", res);

            return res;
        }

        /// <summary>
        /// Sets the cross-chaintransfer status associated with the rejected transaction to <see cref="CrossChainTransferStatus.Rejected"/>.
        /// </summary>
        /// <param name="transaction">The transaction that was rejected.</param>
        public async Task SetRejectedStatusAsync(Transaction transaction)
        {
            this.logger.LogTrace("()");

            IDeposit deposit = this.depositExtractor.ExtractDepositFromTransaction(transaction, 0, 0);
            if (deposit == null)
            {
                this.logger.LogTrace("(-)[NO_DEPOSIT]");
                return;
            }

            CrossChainTransfer crossChainTransfer = (await this.GetAsync(new[] { deposit.Id })).FirstOrDefault();
            if (crossChainTransfer == null)
            {
                this.logger.LogTrace("(-)[NO_TRANSFER]");
                return;
            }

            using (DBreeze.Transactions.Transaction dbreezeTransaction = this.DBreeze.GetTransaction())
            {
                dbreezeTransaction.SynchronizeTables(transferTableName, commonTableName);
                dbreezeTransaction.ValuesLazyLoadingIsOn = false;
                crossChainTransfer.SetStatus(CrossChainTransferStatus.Rejected);
                await this.PutTransferAsync(dbreezeTransaction, crossChainTransfer);
                dbreezeTransaction.Commit();
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Persist the cross-chain transfer information into the database.
        /// </summary>
        /// <param name="dbreezeTransaction">The DBreeze transaction context to use.</param>
        /// <param name="crossChainTransfer">Cross-chain transfer information to be inserted.</param>
        private async Task PutTransferAsync(DBreeze.Transactions.Transaction dbreezeTransaction, CrossChainTransfer crossChainTransfer)
        {
            Guard.NotNull(crossChainTransfer, nameof(crossChainTransfer));

            this.logger.LogTrace("()");

            dbreezeTransaction.Insert<byte[], CrossChainTransfer>(transferTableName, crossChainTransfer.DepositTransactionId.ToBytes(), crossChainTransfer);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Records transfer information from the supplied blocks.
        /// </summary>
        /// <param name="dbreezeTransaction">The DBreeze transaction context to use.</param>
        /// <param name="blockHeight">The block height of the first block in the list.</param>
        /// <param name="blocks">The list of blocks to add.</param>
        private async Task OnInsertBlocksAsync(DBreeze.Transactions.Transaction dbreezeTransaction, int blockHeight, List<Block> blocks)
        {
            // Find transfer transactions in blocks
            foreach (Block block in blocks)
            {
                IReadOnlyList<IDeposit> deposits = this.depositExtractor.ExtractDepositsFromBlock(block, blockHeight);

                // First check the database to see if we already know about these deposits.
                CrossChainTransfer[] storedDeposits = this.Get(dbreezeTransaction, deposits.Select(d => d.Id).ToArray());

                // Update the information about these deposits or record their status.
                for (int i = 0; i < storedDeposits.Length; i++)
                {
                    IDeposit deposit = deposits[i];

                    if (storedDeposits[i] == null)
                    {
                        Script scriptPubKey = BitcoinAddress.Create(deposit.TargetAddress, this.network).ScriptPubKey;
                        Transaction transaction = block.Transactions.Single(t => t.GetHash() == deposit.Id);

                        storedDeposits[i] = new CrossChainTransfer(CrossChainTransferStatus.SeenInBlock, deposit.Id, -1 /* Unknown */,
                            scriptPubKey, deposit.Amount, transaction, block.GetHash(), blockHeight);

                        // Update the lookups.
                        this.depositsIdsByStatus[CrossChainTransferStatus.SeenInBlock].Add(storedDeposits[i].DepositTransactionId);
                        this.depositIdsByBlockHash[block.GetHash()].Add(deposit.Id);
                    }
                    else
                    {
                        // Update the lookups.
                        this.SetTransferStatus(storedDeposits[i], CrossChainTransferStatus.SeenInBlock);
                    }

                    await this.PutTransferAsync(dbreezeTransaction, storedDeposits[i]);
                }

                // Update lookups.
                this.blockHeightsByBlockHash[block.GetHash()] = blockHeight++;
            }
        }

        /// <summary>
        /// Forgets transfer information from the blocks being removed.
        /// </summary>
        /// <param name="dbreezeTransaction">The DBreeze transaction context to use.</param>
        /// <param name="lastBlockHeight">The last block to retain.</param>
        private async Task OnDeleteBlocksAsync(DBreeze.Transactions.Transaction dbreezeTransaction, int lastBlockHeight)
        {
            // Gather all the deposit ids that may have had transactions in the blocks being deleted.
            var depositIds = new HashSet<uint256>();
            uint256[] blocksToRemove = this.blockHeightsByBlockHash.Where(a => a.Value > lastBlockHeight).Select(a => a.Key).ToArray();

            foreach (HashSet<uint256> deposits in blocksToRemove.Select(a => this.depositIdsByBlockHash[a]))
            {
                depositIds.UnionWith(deposits);
            }

            // Find the transfers related to these deposit ids in the database.
            CrossChainTransfer[] crossChainTransfers = this.Get(dbreezeTransaction, depositIds.ToArray());

            foreach (CrossChainTransfer transfer in crossChainTransfers)
            {
                // Transaction is no longer seen.
                this.SetTransferStatus(transfer, CrossChainTransferStatus.FullySigned);

                // Write the transfer status to the database.
                await this.PutTransferAsync(dbreezeTransaction, transfer);

                // Update the lookups.
                this.depositIdsByBlockHash[transfer.BlockHash].Remove(transfer.DepositTransactionId);
            }

            // Update the lookups.
            foreach (uint256 blockHash in blocksToRemove)
            {
                this.blockHeightsByBlockHash.Remove(blockHash);
            }
        }

        /// <summary>
        /// Updates the status of the transfer and the status lookup.
        /// </summary>
        /// <param name="transfer">The cross-chain transfer to update.</param>
        /// <param name="status">The new status.</param>
        private void SetTransferStatus(CrossChainTransfer transfer, CrossChainTransferStatus status)
        {
            this.depositsIdsByStatus[transfer.Status].Remove(transfer.DepositTransactionId);
            transfer.SetStatus(status);
            this.depositsIdsByStatus[transfer.Status].Add(transfer.DepositTransactionId);
        }

        /// <summary>
        /// Verifies that the transaction's input UTXO's have been reserved by the wallet.
        /// </summary>
        /// <param name="transaction">The transaction to check.</param>
        /// <param name="wallet">The wallet to check.</param>
        /// <returns><c>True</c> if all's well and <c>false</c> otherwise.</returns>
        public static bool SanityCheck(Transaction transaction, Wallet.FederationWallet wallet)
        {
            // All the input UTXO's should be present in spending details of the multi-sig address.
            foreach (TxIn input in transaction.Inputs)
            {
                // Only check inputs that the wallet could have seen...
                Wallet.TransactionData transactionData = wallet.MultiSigAddress.Transactions
                    .Where(t => t.SpendingDetails != null && t.SpendingDetails.TransactionId == transaction.GetHash()
                        && t.Id == input.PrevOut.Hash && t.Index == input.PrevOut.N).FirstOrDefault();

                if (transactionData == null)
                    return false;
            }

            return true;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.cancellation.Cancel();
            this.DBreeze.Dispose();
        }
    }
}
