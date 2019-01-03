using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DBreeze;
using DBreeze.Utils;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    /// <summary>
    /// This class provided the low-level cross-chain database functionality which
    /// includes maintaining the various lookups in a transactional manner.
    /// Also see <see cref="CrossChainDBTransaction"/>.
    /// </summary>
    public class CrossChainDB : ICrossChainDB
    {
        /// <summary>This table contains the cross-chain transfer information.</summary>
        public const string TransferTableName = "Transfers";

        /// <summary>This table keeps track of the chain tips so that we know exactly what data our transfer table contains.</summary>
        public const string CommonTableName = "Common";

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>The network.</summary>
        private readonly Network network;

        /// <summary>The chain.</summary>
        private readonly ConcurrentChain chain;

        /// <summary>This contains deposits ids indexed by block hash of the corresponding transaction.</summary>
        protected readonly Dictionary<uint256, HashSet<uint256>> depositIdsByBlockHash = new Dictionary<uint256, HashSet<uint256>>();

        /// <summary>This contains the block heights by block hashes for only the blocks of interest in our chain.</summary>
        protected readonly Dictionary<uint256, int> blockHeightsByBlockHash = new Dictionary<uint256, int>();

        /// <summary>This table contains deposits ids by status.</summary>
        protected readonly Dictionary<CrossChainTransferStatus, HashSet<uint256>> depositsIdsByStatus = new Dictionary<CrossChainTransferStatus, HashSet<uint256>>();

        /// <summary>The block height on the counter-chain for which the next list of deposits is expected.</summary>
        public int NextMatureDepositHeight { get; protected set; }

        /// <summary>The tip of our chain when we last updated the store.</summary>
        public ChainedHeader TipHashAndHeight { get; protected set; }

        /// <summary>The key of the repository tip in the common table.</summary>
        public static readonly byte[] RepositoryTipKey = new byte[] { 0 };

        /// <summary>The key of the counter-chain last mature block tip in the common table.</summary>
        public static readonly byte[] NextMatureTipKey = new byte[] { 1 };

        /// <summary>Access to DBreeze database.</summary>
        private readonly DBreezeEngine DBreeze;

        /// <summary>The DBreeze serializer used to serialize / deserialize objects stored in the DBreeze database.</summary>
        private readonly DBreezeSerializer DBreezeSerializer;

        /// <summary>
        /// Constructs the class controlling the underlying DBreeze database.
        /// </summary>
        /// <param name="network">The network type of the transaction recorded in the database.</param>
        /// <param name="loggerFactory">The logger facrtory used to create the logger.</param>
        /// <param name="chain">The concurrent chain associated with the objects in the database.</param>
        /// <param name="dataFolder">The datafolder where the database files will be persisted.</param>
        /// <param name="federationGatewaySettings">Used to identify the MultiSigAddress that the database is for.</param>
        /// <param name="dbreezeSerializer">The DBreeze serializer used to serialize / deserialize stored objects.</param>
        public CrossChainDB(
            Network network,
            ILoggerFactory loggerFactory,
            ConcurrentChain chain,
            DataFolder dataFolder,
            IFederationGatewaySettings federationGatewaySettings,
            DBreezeSerializer dbreezeSerializer)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(dataFolder, nameof(dataFolder));
            Guard.NotNull(federationGatewaySettings, nameof(federationGatewaySettings));
            Guard.NotNull(dbreezeSerializer, nameof(dbreezeSerializer));

            this.network = network;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.chain = chain;

            Block genesis = network.GetGenesis();
            this.TipHashAndHeight = new ChainedHeader(genesis.Header, genesis.GetHash(), 0);
            this.NextMatureDepositHeight = 1;

            // Future-proof store name.
            string depositStoreName = "federatedTransfers" + federationGatewaySettings.MultiSigAddress.ToString();
            string folder = Path.Combine(dataFolder.RootPath, depositStoreName);
            Directory.CreateDirectory(folder);
            this.DBreeze = new DBreezeEngine(folder);
            this.DBreezeSerializer = dbreezeSerializer;

            // Initialize tracking deposits by status.
            foreach (object status in typeof(CrossChainTransferStatus).GetEnumValues())
                this.depositsIdsByStatus[(CrossChainTransferStatus)status] = new HashSet<uint256>();
        }

        /// <inheritdoc />
        public void Initialize()
        {
            using (CrossChainDBTransaction xdbTransaction = this.GetTransaction())
            {
                this.LoadTipHashAndHeight(xdbTransaction);
                this.LoadNextMatureHeight(xdbTransaction);
                this.logger.LogTrace("Loaded TipHashAndHeight {0} and NextMatureDepositHeight {1}.", this.TipHashAndHeight, this.NextMatureDepositHeight);

                // Initialize the lookups.
                foreach (ICrossChainTransfer transfer in xdbTransaction.EnumerateTransfers())
                {
                    this.depositsIdsByStatus[transfer.Status].Add(transfer.DepositTransactionId);

                    if (transfer.BlockHash != null && transfer.BlockHeight != null)
                    {
                        if (!this.depositIdsByBlockHash.TryGetValue(transfer.BlockHash, out HashSet<uint256> deposits))
                        {
                            deposits = new HashSet<uint256>();
                            this.depositIdsByBlockHash[transfer.BlockHash] = deposits;
                        }

                        deposits.Add(transfer.DepositTransactionId);

                        this.blockHeightsByBlockHash[transfer.BlockHash] = (int)transfer.BlockHeight;
                    }
                }
                this.logger.LogTrace("Lookups initialised.");
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public CrossChainDBTransaction GetTransaction(CrossChainTransactionMode mode = CrossChainTransactionMode.Read)
        {
            CrossChainDBTransaction xdbTransaction = new CrossChainDBTransaction(this.DBreeze, this.DBreezeSerializer, (ICrossChainLookups)this, mode);

            this.logger.LogTrace("Transaction '{0}' created for {1}.", xdbTransaction, mode);

            return xdbTransaction;
        }

        /// <summary>
        /// Gets an array of <see cref="ICrossChainTransfer"/> objects corresponding to the array
        /// of <see cref="uint256"/> deposit ids passed to the method.
        /// </summary>
        /// <param name="xdbTransaction">The <see cref="CrossChainDBTransaction"/> providing the context for the get.</param>
        /// <param name="depositIds">The array of <see cref="uint256"/> deposit ids.</param>
        /// <returns>An array of <see cref="ICrossChainTransfer"/> objects or <c>null</c> for non-existing transfers.</returns>
        protected ICrossChainTransfer[] Get(CrossChainDBTransaction xdbTransaction, uint256[] depositIds)
        {
            try
            {
                // To boost performance we will access the deposits sorted by deposit id.
                var depositDict = new Dictionary<uint256, int>();
                for (int i = 0; i < depositIds.Length; i++)
                    depositDict[depositIds[i]] = i;

                var byteListComparer = new ByteListComparer();
                List<KeyValuePair<uint256, int>> depositList = depositDict.ToList();
                depositList.Sort((pair1, pair2) => byteListComparer.Compare(pair1.Key.ToBytes(), pair2.Key.ToBytes()));

                var res = new ICrossChainTransfer[depositIds.Length];

                foreach (KeyValuePair<uint256, int> kv in depositList)
                {
                    ICrossChainTransfer crossChainTransfer = xdbTransaction.GetTransfer(kv.Key);
                    if (crossChainTransfer != null)
                        res[kv.Value] = crossChainTransfer;
                }

                this.logger.LogTrace("Transaction '{0}' read {1} transfers.", xdbTransaction, res.Length);

                return res;
            }
            catch (Exception err)
            {
                this.logger.LogError("Transaction '{0}' failed with '{1}'.", xdbTransaction, err.Message);
                throw err;
            }
        }

        /// <summary>Persist multiple cross-chain transfer information into the database.</summary>
        /// <param name="xdbTransaction">The crosschain database transaction context to use.</param>
        /// <param name="crossChainTransfers">Cross-chain transfers to be inserted.</param>
        protected void PutTransfers(CrossChainDBTransaction xdbTransaction, ICrossChainTransfer[] crossChainTransfers)
        {
            try
            {
                // Optimal ordering for DB consumption.
                var byteListComparer = new ByteListComparer();
                List<ICrossChainTransfer> orderedTransfers = crossChainTransfers.ToList();
                orderedTransfers.Sort((pair1, pair2) => byteListComparer.Compare(pair1.DepositTransactionId.ToBytes(), pair2.DepositTransactionId.ToBytes()));

                // Write each transfer in order.
                foreach (ICrossChainTransfer transfer in orderedTransfers)
                {
                    xdbTransaction.PutTransfer(transfer);
                }

                this.logger.LogTrace("Transaction '{0}' updated {1} transfers.", xdbTransaction, orderedTransfers.Count);
            }
            catch (Exception err)
            {
                this.logger.LogError("Transaction '{0}' failed with '{1}'.", xdbTransaction, err.Message);
                throw err;
            }
        }

        /// <summary>Rolls back the database if an operation running in the context of a database transaction fails.</summary>
        /// <param name="xdbTransaction">Database transaction to roll back.</param>
        /// <param name="exception">Exception to report and re-raise.</param>
        /// <param name="reason">Short reason/context code of failure.</param>
        protected void RollbackAndThrowTransactionError(CrossChainDBTransaction xdbTransaction, Exception exception, string reason = "FAILED_TRANSACTION")
        {
            this.logger.LogError("Error during database update: {0}", exception.Message);
            this.logger.LogTrace("(-):[{0}]", reason);

            xdbTransaction.Rollback();
            throw exception;
        }

        /// <summary>Loads the tip and hash height.</summary>
        /// <param name="xdbTransaction">The DBreeze transaction context to use.</param>
        /// <returns>The hash and height pair.</returns>
        protected ChainedHeader LoadTipHashAndHeight(CrossChainDBTransaction xdbTransaction)
        {
            try
            {
                BlockLocator blockLocator = xdbTransaction.LoadTipHashAndHeight();

                this.TipHashAndHeight = this.chain.GetBlock(blockLocator.Blocks[0]) ?? this.chain.FindFork(blockLocator);

                this.logger.LogTrace("Transaction '{0}' read TipHashAndHeight {1}.", xdbTransaction, this.TipHashAndHeight);

                return this.TipHashAndHeight;
            }
            catch (Exception err)
            {
                this.logger.LogError("Transaction '{0}' failed with '{1}'.", xdbTransaction, err.Message);
                throw err;
            }
        }

        /// <summary>Saves the tip and hash height.</summary>
        /// <param name="xdbTransaction">The crosschain db transaction context to use.</param>
        /// <param name="newTip">The new tip to persist.</param>
        protected void SaveTipHashAndHeight(CrossChainDBTransaction xdbTransaction, ChainedHeader newTip)
        {
            try
            {
                BlockLocator locator = this.chain.GetBlock(newTip.HashBlock).GetLocator();
                xdbTransaction.SaveTipHashAndHeight(locator);
                this.TipHashAndHeight = newTip;

                this.logger.LogTrace("Transaction '{0}' set TipHashAndHeight {1}", xdbTransaction, newTip);
            }
            catch (Exception err)
            {
                this.logger.LogError("Transaction '{0}' failed with '{1}'.", xdbTransaction, err.Message);
                throw err;
            }
        }

        /// <summary>Loads the counter-chain next mature block height.</summary>
        /// <param name="xdbTransaction">The crosschain db transaction context to use.</param>
        /// <returns>The hash and height pair.</returns>
        protected int LoadNextMatureHeight(CrossChainDBTransaction xdbTransaction)
        {
            try
            {
                int height = xdbTransaction.LoadNextMatureHeight() ?? this.NextMatureDepositHeight;

                this.logger.LogTrace("Transaction '{0}' read NextMatureDepositHeight {1}", xdbTransaction, height);

                return height;
            }
            catch (Exception err)
            {
                this.logger.LogError("Transaction '{0}' failed with '{1}'.", xdbTransaction, err.Message);
                throw err;
            }
        }

        /// <summary>Saves the counter-chain next mature block height.</summary>
        /// <param name="xdbTransaction">The crosschain db transaction context to use.</param>
        /// <param name="newTip">The next mature block height on the counter-chain.</param>
        protected void SaveNextMatureHeight(CrossChainDBTransaction xdbTransaction, int newTip)
        {
            try
            {
                xdbTransaction.SaveNextMatureHeight(newTip);
                this.NextMatureDepositHeight = newTip;

                this.logger.LogTrace("Transaction '{0}' set NextMatureDepositHeight {1}", newTip);
            }
            catch (Exception err)
            {
                this.logger.LogError("Transaction '{0}' failed with '{1}'.", xdbTransaction, err.Message);
                throw err;
            }
        }

        /// <inheritdoc />
        public Dictionary<Type, IChangeTracker> CreateTrackers()
        {
            var trackers = new Dictionary<Type, IChangeTracker>();

            trackers[typeof(ICrossChainTransfer)] = new StatusChangeTracker();

            return trackers;
        }

        /// <inheritdoc />
        public void UpdateLookups(Dictionary<Type, IChangeTracker> trackers)
        {
            StatusChangeTracker tracker = (StatusChangeTracker)trackers[typeof(ICrossChainTransfer)];

            foreach (uint256 hash in tracker.UniqueBlockHashes())
            {
                if (this.depositIdsByBlockHash.ContainsKey(hash)) continue;
                this.depositIdsByBlockHash[hash] = new HashSet<uint256>();
            }

            foreach (KeyValuePair<ICrossChainTransfer, CrossChainTransferStatus?> kv in tracker)
            {
                ICrossChainTransfer transfer = kv.Key;

                if (transfer.DepositHeight == null && transfer.DbStatus != null /* Not new */)
                {
                    // Transfer is being removed.
                    this.depositsIdsByStatus[transfer.Status].Remove(transfer.DepositTransactionId);
                    this.depositIdsByBlockHash[transfer.BlockHash].Remove(transfer.DepositTransactionId);
                }
                else
                {
                    this.TransferStatusUpdated(transfer, tracker[transfer]);

                    if (transfer.BlockHash != null && transfer.BlockHeight != null)
                    {
                        if (!this.depositIdsByBlockHash[transfer.BlockHash].Contains(transfer.DepositTransactionId))
                            this.depositIdsByBlockHash[transfer.BlockHash].Add(transfer.DepositTransactionId);
                        this.blockHeightsByBlockHash[transfer.BlockHash] = (int)transfer.BlockHeight;
                    }
                }
            }

            this.logger.LogTrace("Lookups updated from tracker containing {0} items.", tracker.Count);
        }

        /// <summary>Updates the status lookup based on a transfer and its previous status.</summary>
        /// <param name="transfer">The cross-chain transfer that was updated.</param>
        /// <param name="oldStatus">The old status.</param>
        private void TransferStatusUpdated(ICrossChainTransfer transfer, CrossChainTransferStatus? oldStatus)
        {
            if (oldStatus != null)
            {
                this.depositsIdsByStatus[(CrossChainTransferStatus)oldStatus].Remove(transfer.DepositTransactionId);
            }

            this.depositsIdsByStatus[transfer.Status].Add(transfer.DepositTransactionId);
        }

        public virtual void Dispose()
        {
            this.DBreeze.Dispose();
        }
    }
}
