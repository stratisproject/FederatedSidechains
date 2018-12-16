using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Wallet;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    public class CrossChainTransferStore : CrossChainDB, ICrossChainTransferStore
    {
        // <summary>Block batch size for synchronization</summary>
        private const int synchronizationBatchSize = 1000;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;
        private readonly Network network;
        private readonly ConcurrentChain chain;
        private readonly IWithdrawalExtractor withdrawalExtractor;
        private readonly IBlockRepository blockRepository;
        private readonly CancellationTokenSource cancellation;
        private readonly IFederationWalletManager federationWalletManager;
        private readonly IFederationWalletTransactionHandler federationWalletTransactionHandler;
        private readonly IFederationGatewaySettings federationGatewaySettings;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly object lockObj;

        public CrossChainTransferStore(Network network, DataFolder dataFolder, ConcurrentChain chain, IFederationGatewaySettings settings, IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory, IWithdrawalExtractor withdrawalExtractor, IFullNode fullNode, IBlockRepository blockRepository,
            IFederationWalletManager federationWalletManager, IFederationWalletTransactionHandler federationWalletTransactionHandler)
            : base(network, loggerFactory, chain, dataFolder, settings)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(dataFolder, nameof(dataFolder));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(settings, nameof(settings));
            Guard.NotNull(dateTimeProvider, nameof(dateTimeProvider));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(withdrawalExtractor, nameof(withdrawalExtractor));
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
            this.federationGatewaySettings = settings;
            this.withdrawalExtractor = withdrawalExtractor;
            this.lockObj = new object();
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.TipHashAndHeight = this.chain.GetBlock(0);
            this.NextMatureDepositHeight = 1;
            this.cancellation = new CancellationTokenSource();
        }

        /// <summary>Starts the cross-chain-transfer store.</summary>
        public void Start()
        {
            lock (this.lockObj)
            {
                // Remove all transient transactions from the wallet to be re-added according to the
                // information carried in the store. This ensures that we will re-sync in the case
                // where the store may have been deleted.
                // Any partial transfers affected by these removals are expected to first become
                // suspended due to the missing wallet transactions which will rewind the counter-
                // chain tip to then reprocess them.
                if (this.federationWalletManager.RemoveTransientTransactions())
                {
                    this.logger.LogTrace("Unseen transactions have been removed from the wallet.");
                    this.federationWalletManager.SaveWallet();
                }

                Guard.Assert(this.Synchronize());

                this.logger.LogTrace("Adding any missing but seen transactions to wallet.");

                FederationWallet wallet = this.federationWalletManager.GetWallet();
                ICrossChainTransfer[] transfers = Get(this.depositsIdsByStatus[CrossChainTransferStatus.SeenInBlock].ToArray());
                foreach (ICrossChainTransfer transfer in transfers)
                {
                    (Transaction tran, TransactionData tranData, _) = this.federationWalletManager.FindWithdrawalTransactions(transfer.DepositTransactionId).FirstOrDefault();
                    if (tran == null && wallet.LastBlockSyncedHeight >= transfer.BlockHeight)
                    {
                        this.federationWalletManager.ProcessTransaction(transfer.PartialTransaction);
                        (tran, tranData, _) = this.federationWalletManager.FindWithdrawalTransactions(transfer.DepositTransactionId).FirstOrDefault();
                        tranData.BlockHeight = transfer.BlockHeight;
                        tranData.BlockHash = transfer.BlockHash;
                    }
                }
            }
        }

        /// <inheritdoc />
        public bool HasSuspended()
        {
            return this.depositsIdsByStatus[CrossChainTransferStatus.Suspended].Count != 0;
        }

        /// <inheritdoc />
        public bool CanPersistMatureDeposits()
        {
            return this.federationWalletManager.IsFederationActive();
        }

        /// <summary>
        /// The store will chase the wallet tip. This will ensure that we can rely on
        /// information recorded in the wallet such as the list of unspent UTXO's.
        /// </summary>
        /// <returns>The height to which the wallet has been synced.</returns>
        private HashHeightPair TipToChase()
        {
            FederationWallet wallet = this.federationWalletManager.GetWallet();

            if (wallet?.LastBlockSyncedHeight == null)
            {
                return new HashHeightPair(this.network.GenesisHash, 0);
            }

            return new HashHeightPair(wallet.LastBlockSyncedHash, (int)wallet.LastBlockSyncedHeight);
        }

        /// <summary>
        /// Partial or fully signed transfers should have their source UTXO's recorded by an up-to-date wallet.
        /// Sets transfers to <see cref="CrossChainTransferStatus.Suspended"/> if their UTXO's are not reserved
        /// within the wallet.
        /// </summary>
        /// <param name="crossChainTransfers">The transfers to check. If not supplied then all partial and fully signed transfers are checked.</param>
        /// <returns>Returns the list of transfers, possible with updated statuses.</returns>
        private ICrossChainTransfer[] ValidateCrossChainTransfers(ICrossChainTransfer[] crossChainTransfers = null)
        {
            FederationWallet wallet = this.federationWalletManager.GetWallet();

            if (crossChainTransfers == null)
            {
                crossChainTransfers = Get(
                    this.depositsIdsByStatus[CrossChainTransferStatus.Partial].Union(
                        this.depositsIdsByStatus[CrossChainTransferStatus.FullySigned]).ToArray());
            }

            var tracker = new StatusChangeTracker();
            int newChainATip = this.NextMatureDepositHeight;

            foreach (ICrossChainTransfer partialTransfer in crossChainTransfers)
            {
                if (partialTransfer == null)
                    continue;

                if (partialTransfer.Status != CrossChainTransferStatus.Partial && partialTransfer.Status != CrossChainTransferStatus.FullySigned)
                    continue;

                List<(Transaction, TransactionData, IWithdrawal)> walletData = this.federationWalletManager.FindWithdrawalTransactions(partialTransfer.DepositTransactionId);
                if (walletData.Count == 1 && ValidateTransaction(walletData[0].Item1))
                {
                    Transaction walletTran = walletData[0].Item1;
                    if (walletTran.GetHash() == partialTransfer.PartialTransaction.GetHash())
                        continue;

                    if (CrossChainTransfer.TemplatesMatch(walletTran, partialTransfer.PartialTransaction))
                    {
                        this.logger.LogTrace("Could not find transaction by hash {0} but found it by matching template.", partialTransfer.PartialTransaction.GetHash());
                        this.logger.LogTrace("Will update transfer with wallet transaction {0}.", walletTran.GetHash());

                        partialTransfer.SetPartialTransaction(walletTran);

                        if (walletData[0].Item2.BlockHeight != null)
                            tracker.SetTransferStatus(partialTransfer, CrossChainTransferStatus.SeenInBlock, walletData[0].Item2.BlockHash, (int)walletData[0].Item2.BlockHeight);
                        else if (ValidateTransaction(walletTran, true))
                            tracker.SetTransferStatus(partialTransfer, CrossChainTransferStatus.FullySigned);
                        else
                            tracker.SetTransferStatus(partialTransfer, CrossChainTransferStatus.Partial);

                        continue;
                    }
                }

                // The chain may have been rewound so that this transaction or its UTXO's have been lost.
                // Rewind our recorded chain A tip to ensure the transaction is re-built once UTXO's become available.
                if (partialTransfer.DepositHeight != null && partialTransfer.DepositHeight < newChainATip)
                {
                    newChainATip = (int)partialTransfer.DepositHeight;

                    this.logger.LogTrace("Will rewind NextMatureDepositHeight due to suspended deposit {0} at height {1}.",
                        partialTransfer.DepositTransactionId, newChainATip);
                }

                tracker.SetTransferStatus(partialTransfer, CrossChainTransferStatus.Suspended);
            }

            // Exit if nothing to do.
            if (tracker.Count == 0)
                return crossChainTransfers;

            using (CrossChainDBTransaction xdbTransaction = this.GetTransaction(CrossChainTransactionMode.ReadWrite))
            {
                int oldChainATip = this.NextMatureDepositHeight;

                try
                {
                    foreach (KeyValuePair<ICrossChainTransfer, CrossChainTransferStatus?> kv in tracker)
                    {
                        xdbTransaction.PutTransfer(kv.Key);
                    }

                    xdbTransaction.SaveNextMatureHeight(newChainATip);
                    xdbTransaction.Commit();

                    bool walletUpdated = false;

                    // Remove any remnants of suspended transactions from the wallet.
                    foreach (KeyValuePair<ICrossChainTransfer, CrossChainTransferStatus?> kv in tracker)
                    {
                        if (kv.Value == CrossChainTransferStatus.Suspended)
                        {
                            walletUpdated |= this.federationWalletManager.RemoveTransientTransactions(kv.Key.DepositTransactionId);
                        }
                    }

                    // Remove transient transactions after the next mature deposit height.
                    foreach ((Transaction, TransactionData, IWithdrawal) t in this.federationWalletManager.FindWithdrawalTransactions())
                    {
                        if (t.Item3.BlockNumber >= newChainATip)
                        {
                            walletUpdated |= this.federationWalletManager.RemoveTransientTransactions(t.Item3.DepositId);
                        }
                    }

                    if (walletUpdated)
                        this.federationWalletManager.SaveWallet();

                    return crossChainTransfers;
                }
                catch (Exception err)
                {
                    // Restore expected store state in case the calling code retries / continues using the store.
                    this.NextMatureDepositHeight = oldChainATip;

                    this.RollbackAndThrowTransactionError(xdbTransaction, err, "SANITY_ERROR");

                    // Dummy return as the above method throws. Avoids compiler error.
                    return null;
                }
            }
        }

        private Transaction BuildDeterministicTransaction(uint256 depositId, Recipient recipient)
        {
            try
            {
                // Build the multisig transaction template.
                uint256 opReturnData = depositId;
                string walletPassword = this.federationWalletManager.Secret.WalletPassword;
                var multiSigContext = new TransactionBuildContext(new[] { recipient }.ToList(), opReturnData: opReturnData.ToBytes())
                {
                    OrderCoinsDeterministic = true,
                    TransactionFee = this.federationGatewaySettings.TransactionFee,
                    MinConfirmations = this.federationGatewaySettings.MinCoinMaturity,
                    Shuffle = false,
                    IgnoreVerify = true,
                    WalletPassword = walletPassword,
                    Sign = (walletPassword ?? "") != ""
                };

                // Build the transaction.
                Transaction transaction = this.federationWalletTransactionHandler.BuildTransaction(multiSigContext);

                if (transaction == null)
                    this.logger.LogTrace("Failed to create deterministic transaction.");
                else
                    this.logger.LogTrace("Deterministic transaction created.");

                return transaction;
            }
            catch (Exception error)
            {
                this.logger.LogTrace("Could not create transaction for deposit {0}: {1}", depositId, error.Message);
            }

            return null;
        }

        /// <inheritdoc />
        public Task SaveCurrentTipAsync()
        {
            return Task.Run(() =>
            {
                lock (this.lockObj)
                {
                    using (CrossChainDBTransaction xdbTransaction = this.GetTransaction(CrossChainTransactionMode.ReadWrite))
                    {
                        xdbTransaction.SaveNextMatureHeight(this.NextMatureDepositHeight);
                        xdbTransaction.Commit();
                    }
                }
            });
        }

        /// <inheritdoc />
        public Task<bool> RecordLatestMatureDepositsAsync(IMaturedBlockDeposits[] maturedBlockDeposits)
        {
            Guard.NotNull(maturedBlockDeposits, nameof(maturedBlockDeposits));
            Guard.Assert(!maturedBlockDeposits.Any(m => m.Deposits.Any(d => d.BlockNumber != m.Block.BlockHeight || d.BlockHash != m.Block.BlockHash)));

            return Task.Run(() =>
            {
                lock (this.lockObj)
                {
                    this.logger.LogTrace("()");

                    // Sanitize and sort the list.
                    int originalDepositHeight = this.NextMatureDepositHeight;

                    maturedBlockDeposits = maturedBlockDeposits
                        .OrderBy(a => a.Block.BlockHeight)
                        .SkipWhile(m => m.Block.BlockHeight < this.NextMatureDepositHeight).ToArray();

                    if (maturedBlockDeposits.Length == 0 ||
                        maturedBlockDeposits.First().Block.BlockHeight != this.NextMatureDepositHeight)
                    {
                        this.logger.LogTrace("No block found starting at height {0}.", this.NextMatureDepositHeight);
                        this.logger.LogTrace("(-):[NO_VIABLE_BLOCKS]");
                        return true;
                    }

                    if (maturedBlockDeposits.Last().Block.BlockHeight != this.NextMatureDepositHeight + maturedBlockDeposits.Length - 1)
                    {
                        this.logger.LogTrace("Input containing duplicate blocks will be ignored.");
                        this.logger.LogTrace("(-):[DUPLICATE_BLOCKS]");
                        return true;
                    }

                    this.Synchronize();

                    FederationWallet wallet = this.federationWalletManager.GetWallet();
                    bool? canPersist = null;

                    for (int j = 0; j < maturedBlockDeposits.Length; j++)
                    {
                        if (maturedBlockDeposits[j].Block.BlockHeight != this.NextMatureDepositHeight)
                            continue;

                        IReadOnlyList<IDeposit> deposits = maturedBlockDeposits[j].Deposits;
                        if (deposits.Count == 0)
                        {
                            this.NextMatureDepositHeight++;
                            continue;
                        }

                        // CanPersistMatureDeposits is a bit slow. Call it only once.
                        canPersist = canPersist ?? this.CanPersistMatureDeposits();

                        if (!(bool)canPersist)
                        {
                            this.logger.LogError("The store can't persist mature deposits at the moment.");
                            this.logger.LogTrace("(-)");
                            continue;
                        }

                        ICrossChainTransfer[] transfers = this.ValidateCrossChainTransfers(this.Get(deposits.Select(d => d.Id).ToArray()));
                        var tracker = new StatusChangeTracker();
                        bool walletUpdated = false;

                        // Deposits are assumed to be in order of occurrence on the source chain.
                        // If we fail to build a transacion the transfer and subsequent transfers
                        // in the orderd list will be set to suspended.
                        bool haveSuspendedTransfers = false;

                        for (int i = 0; i < deposits.Count; i++)
                        {
                            // Only do work for non-existing or suspended transfers.
                            if (transfers[i] != null && transfers[i].Status != CrossChainTransferStatus.Suspended)
                            {
                                continue;
                            }

                            IDeposit deposit = deposits[i];
                            Transaction transaction = null;
                            var status = CrossChainTransferStatus.Suspended;
                            Script scriptPubKey = BitcoinAddress.Create(deposit.TargetAddress, this.network).ScriptPubKey;

                            if (!haveSuspendedTransfers)
                            {
                                var recipient = new Recipient
                                {
                                    Amount = deposit.Amount,
                                    ScriptPubKey = scriptPubKey
                                };

                                transaction = BuildDeterministicTransaction(deposit.Id, recipient);

                                if (transaction != null)
                                {
                                    this.logger.LogTrace("Reserving the UTXOs before building the next transaction.");

                                    walletUpdated |= this.federationWalletManager.ProcessTransaction(transaction, isPropagated: false);

                                    status = CrossChainTransferStatus.Partial;
                                }
                                else
                                {
                                    haveSuspendedTransfers = true;
                                }
                            }

                            if (transfers[i] == null || transaction == null)
                            {
                                transfers[i] = new CrossChainTransfer(status, deposit.Id, scriptPubKey, deposit.Amount, deposit.BlockNumber, transaction, null, null);
                                tracker.SetTransferStatus(transfers[i]);
                            }
                            else
                            {
                                transfers[i].SetPartialTransaction(transaction);
                                tracker.SetTransferStatus(transfers[i], CrossChainTransferStatus.Partial);
                            }
                        }

                        using (CrossChainDBTransaction xdbTransaction = this.GetTransaction(CrossChainTransactionMode.ReadWrite))
                        {
                            int currentDepositHeight = this.NextMatureDepositHeight;

                            try
                            {
                                if (walletUpdated)
                                {
                                    this.federationWalletManager.SaveWallet();
                                }

                                // Update new or modified transfers.
                                foreach (KeyValuePair<ICrossChainTransfer, CrossChainTransferStatus?> kv in tracker)
                                {
                                    ICrossChainTransfer transfer = kv.Key;

                                    this.logger.LogTrace("Registering transfer: {0}.", transfer);

                                    xdbTransaction.PutTransfer(transfer);
                                }

                                // Ensure we get called for a retry by NOT advancing the chain A tip if the block
                                // contained any suspended transfers.
                                if (!haveSuspendedTransfers)
                                {
                                    this.SaveNextMatureHeight(xdbTransaction, this.NextMatureDepositHeight + 1);
                                }

                                xdbTransaction.Commit();
                            }
                            catch (Exception err)
                            {
                                this.logger.LogTrace("Undoing reserved UTXOs.");

                                if (walletUpdated)
                                {
                                    foreach (KeyValuePair<ICrossChainTransfer, CrossChainTransferStatus?> kv in tracker)
                                    {
                                        if (kv.Value == CrossChainTransferStatus.Partial)
                                        {
                                            this.federationWalletManager.RemoveTransientTransactions(kv.Key.DepositTransactionId);
                                        }
                                    }

                                    this.federationWalletManager.SaveWallet();
                                }

                                // Restore expected store state in case the calling code retries / continues using the store.
                                this.NextMatureDepositHeight = currentDepositHeight;
                                this.RollbackAndThrowTransactionError(xdbTransaction, err, "DEPOSIT_ERROR");
                            }
                        }
                    }

                    this.logger.LogTrace("(-)");

                    // If progress was made we will check for more blocks.
                    return this.NextMatureDepositHeight != originalDepositHeight;
                }
            });
        }

        /// <inheritdoc />
        public Task<Transaction> MergeTransactionSignaturesAsync(uint256 depositId, Transaction[] partialTransactions)
        {
            Guard.NotNull(depositId, nameof(depositId));
            Guard.NotNull(partialTransactions, nameof(partialTransactions));

            return Task.Run(() =>
            {
                lock (this.lockObj)
                {
                    this.logger.LogTrace("()");
                    this.Synchronize();

                    FederationWallet wallet = this.federationWalletManager.GetWallet();
                    ICrossChainTransfer transfer = this.ValidateCrossChainTransfers(this.Get(new[] { depositId })).FirstOrDefault();

                    if (transfer == null)
                    {
                        this.logger.LogTrace("(-)[MERGE_NOTFOUND]");
                        return null;
                    }

                    if (transfer.Status != CrossChainTransferStatus.Partial)
                    {
                        this.logger.LogTrace("(-)[MERGE_BADSTATUS]");
                        return transfer.PartialTransaction;
                    }

                    var builder = new TransactionBuilder(this.network);
                    Transaction oldTransaction = transfer.PartialTransaction;

                    transfer.CombineSignatures(builder, partialTransactions);

                    if (transfer.PartialTransaction.GetHash() == oldTransaction.GetHash())
                    {
                        this.logger.LogTrace("(-)[MERGE_UNCHANGED]");
                        return transfer.PartialTransaction;
                    }

                    using (CrossChainDBTransaction xdbTransaction = this.GetTransaction(CrossChainTransactionMode.ReadWrite))
                    {
                        try
                        {
                            this.federationWalletManager.ProcessTransaction(transfer.PartialTransaction);
                            this.federationWalletManager.SaveWallet();

                            if (ValidateTransaction(transfer.PartialTransaction, true))
                            {
                                transfer.SetStatus(CrossChainTransferStatus.FullySigned);
                            }

                            xdbTransaction.PutTransfer(transfer);
                            xdbTransaction.Commit();
                        }
                        catch (Exception err)
                        {
                            // Restore expected store state in case the calling code retries / continues using the store.
                            transfer.SetPartialTransaction(oldTransaction);
                            this.federationWalletManager.ProcessTransaction(oldTransaction);
                            this.federationWalletManager.SaveWallet();
                            this.RollbackAndThrowTransactionError(xdbTransaction, err, "MERGE_ERROR");
                        }

                        this.logger.LogTrace("(-)");
                        return transfer?.PartialTransaction;
                    }
                }
            });
        }

        /// <summary>
        /// Uses the information contained in our chain's blocks to update the store.
        /// Sets the <see cref="CrossChainTransferStatus.SeenInBlock"/> status for transfers
        /// identified in the blocks.
        /// </summary>
        /// <param name="blocks">The blocks used to update the store. Must be sorted by ascending height leading up to the new tip.</param>
        private void Put(List<Block> blocks)
        {
            this.logger.LogTrace("Putting {0} blocks.", blocks.Count);

            if (blocks.Count == 0)
                this.logger.LogTrace("(-):0");

            Dictionary<uint256, ICrossChainTransfer> transferLookup;
            Dictionary<uint256, IWithdrawal[]> allWithdrawals;
            {
                int blockHeight = this.TipHashAndHeight.Height + 1;
                var allDepositIds = new HashSet<uint256>();

                allWithdrawals = new Dictionary<uint256, IWithdrawal[]>();
                foreach (Block block in blocks)
                {
                    IReadOnlyList<IWithdrawal> blockWithdrawals = this.withdrawalExtractor.ExtractWithdrawalsFromBlock(block, blockHeight++);
                    allDepositIds.UnionWith(blockWithdrawals.Select(d => d.DepositId).ToArray());
                    allWithdrawals[block.GetHash()] = blockWithdrawals.ToArray();
                }

                // Nothing to do?
                if (allDepositIds.Count == 0)
                {
                    // Exiting here and saving the tip after the sync.
                    this.TipHashAndHeight = this.chain.GetBlock(blocks.Last().GetHash());

                    this.logger.LogTrace("(-)[NO_DEPOSITS]");
                    return;
                }

                // Create transfer lookup by deposit Id.
                uint256[] uniqueDepositIds = allDepositIds.ToArray();
                ICrossChainTransfer[] uniqueTransfers = this.Get(uniqueDepositIds);
                transferLookup = new Dictionary<uint256, ICrossChainTransfer>();
                for (int i = 0; i < uniqueDepositIds.Length; i++)
                    transferLookup[uniqueDepositIds[i]] = uniqueTransfers[i];
            }

            // Find transfer transactions in blocks
            foreach (Block block in blocks)
            {
                // First check the database to see if we already know about these deposits.
                IWithdrawal[] withdrawals = allWithdrawals[block.GetHash()].ToArray();
                ICrossChainTransfer[] crossChainTransfers = withdrawals.Select(d => transferLookup[d.DepositId]).ToArray();

                // Update the information about these deposits or record their status.
                for (int i = 0; i < crossChainTransfers.Length; i++)
                {
                    IWithdrawal withdrawal = withdrawals[i];
                    Transaction transaction = block.Transactions.Single(t => t.GetHash() == withdrawal.Id);

                    // Ensure that the wallet is in step.
                    this.federationWalletManager.ProcessTransaction(transaction, withdrawal.BlockNumber, block);

                    if (crossChainTransfers[i] == null)
                    {
                        Script scriptPubKey = BitcoinAddress.Create(withdrawal.TargetAddress, this.network).ScriptPubKey;

                        crossChainTransfers[i] = new CrossChainTransfer(CrossChainTransferStatus.SeenInBlock, withdrawal.DepositId,
                            scriptPubKey, withdrawal.Amount, null, transaction, withdrawal.BlockHash, withdrawal.BlockNumber);

                        transferLookup[crossChainTransfers[i].DepositTransactionId] = crossChainTransfers[i];
                    }
                    else
                    {
                        crossChainTransfers[i].SetPartialTransaction(transaction);
                        crossChainTransfers[i].SetStatus(CrossChainTransferStatus.SeenInBlock, withdrawal.BlockHash, withdrawal.BlockNumber);
                    }
                }
            }

            // Only create a transaction if there is work to do.
            if (transferLookup.Count == 0)
            {
                this.logger.LogTrace("(-)[NOTHING_TO_DO]");
                return;
            }

            using (CrossChainDBTransaction xdbTransaction = this.GetTransaction(CrossChainTransactionMode.ReadWrite))
            {
                ChainedHeader prevTip = this.TipHashAndHeight;

                try
                {
                    // Write transfers.
                    this.PutTransfers(xdbTransaction, transferLookup.Select(x => x.Value).ToArray());

                    // Commit additions
                    ChainedHeader newTip = this.chain.GetBlock(blocks.Last().GetHash());
                    this.SaveTipHashAndHeight(xdbTransaction, newTip);
                    xdbTransaction.Commit();
                }
                catch (Exception err)
                {
                    // Restore expected store state in case the calling code retries / continues using the store.
                    this.TipHashAndHeight = prevTip;
                    this.RollbackAndThrowTransactionError(xdbTransaction, err, "PUT_ERROR");
                }
            }

            this.logger.LogTrace("(-):{0}", blocks.Count);
        }

        /// <summary>
        /// Used to handle reorg (if required) and revert status from <see cref="CrossChainTransferStatus.SeenInBlock"/> to
        /// <see cref="CrossChainTransferStatus.FullySigned"/>. Also returns a flag to indicate whether we are behind the current tip.
        /// </summary>
        /// <returns>Returns <c>true</c> if a rewind was performed and <c>false</c> otherwise.</returns>
        private bool RewindIfRequired()
        {
            HashHeightPair tipToChase = this.TipToChase();

            if (tipToChase.Hash == this.TipHashAndHeight.HashBlock)
            {
                // Indicate that we are synchronized.
                this.logger.LogTrace("(-):false");
                return false;
            }

            this.logger.LogTrace("Rewinding.");

            // We are dependent on the wallet manager having dealt with any fork by now.
            if (this.chain.GetBlock(tipToChase.Hash) == null)
            {
                this.logger.LogTrace("The wallet tip is not found in the chain. Rewinding on behalf of wallet.");

                ICollection<uint256> locators = this.federationWalletManager.GetWallet().BlockLocator;
                var blockLocator = new BlockLocator { Blocks = locators.ToList() };
                ChainedHeader fork = this.chain.FindFork(blockLocator);
                this.federationWalletManager.RemoveBlocks(fork);
                tipToChase = this.TipToChase();
            }

            // If the chain does not contain our tip..
            if (this.TipHashAndHeight != null && (this.TipHashAndHeight.Height > tipToChase.Height ||
                this.chain.GetBlock(this.TipHashAndHeight.HashBlock)?.Height != this.TipHashAndHeight.Height))
            {
                this.logger.LogTrace("The chain does not contain our tip.");

                // We are ahead of the current chain or on the wrong chain.
                ChainedHeader fork = this.chain.FindFork(this.TipHashAndHeight.GetLocator()) ?? this.chain.GetBlock(0);

                // Must not exceed wallet height otherise transaction validations may fail.
                while (fork.Height > tipToChase.Height)
                    fork = fork.Previous;

                this.logger.LogTrace("Fork height determined to be {0}", fork.Height);

                using (CrossChainDBTransaction xdbTransaction = this.GetTransaction(CrossChainTransactionMode.ReadWrite))
                {
                    ChainedHeader prevTip = this.TipHashAndHeight;

                    try
                    {
                        this.OnDeleteBlocks(xdbTransaction, fork.Height);
                        this.SaveTipHashAndHeight(xdbTransaction, fork);
                        xdbTransaction.Commit();
                    }
                    catch (Exception err)
                    {
                        // Restore expected store state in case the calling code retries / continues using the store.
                        this.TipHashAndHeight = prevTip;
                        this.RollbackAndThrowTransactionError(xdbTransaction, err, "REWIND_ERROR");
                    }
                }

                this.ValidateCrossChainTransfers();
                this.logger.LogTrace("(-):true");
                return true;
            }

            // Indicate that we are behind the current chain.
            this.logger.LogTrace("(-):false");
            return false;
        }

        /// <summary>Attempts to synchronizes the store with the chain.</summary>
        /// <returns>Returns <c>true</c> if the store is in sync or <c>false</c> otherwise.</returns>
        private bool Synchronize()
        {
            lock (this.lockObj)
            {
                this.logger.LogTrace("Synchronizing.");

                HashHeightPair tipToChase = this.TipToChase();
                if (tipToChase.Hash == this.TipHashAndHeight.HashBlock)
                {
                    // Indicate that we are synchronized.
                    this.logger.LogTrace("(-):true");
                    return true;
                }

                while (!this.cancellation.IsCancellationRequested)
                {
                    if (this.HasSuspended())
                    {
                        ICrossChainTransfer[] transfers = this.Get(this.depositsIdsByStatus[CrossChainTransferStatus.Suspended].ToArray());
                        this.NextMatureDepositHeight = transfers.Min(t => t.DepositHeight) ?? this.NextMatureDepositHeight;
                    }

                    this.RewindIfRequired();

                    if (this.SynchronizeBatch())
                    {
                        using (CrossChainDBTransaction xdbTransaction = this.GetTransaction(CrossChainTransactionMode.ReadWrite))
                        {
                            this.SaveTipHashAndHeight(xdbTransaction, this.TipHashAndHeight);

                            xdbTransaction.Commit();
                        }

                        this.logger.LogTrace("(-):true");
                        return true;
                    }
                }

                this.logger.LogTrace("(-):false");
                return false;
            }
        }

        /// <summary>Synchronize with a batch of blocks.</summary>
        /// <returns>Returns <c>true</c> if we match the chain tip and <c>false</c> if we are behind the tip.</returns>
        private bool SynchronizeBatch()
        {
            // Get a batch of blocks.
            var blockHashes = new List<uint256>();
            int batchSize = 0;
            HashHeightPair tipToChase = this.TipToChase();

            foreach (ChainedHeader header in this.chain.EnumerateToTip(this.TipHashAndHeight.HashBlock).Skip(1))
            {
                if (this.chain.GetBlock(header.HashBlock) == null)
                    break;

                if (header.Height > tipToChase.Height)
                    break;

                blockHashes.Add(header.HashBlock);

                if (++batchSize >= synchronizationBatchSize)
                    break;
            }

            this.logger.LogTrace("Attempting to synchronize a batch of {0} blocks.", batchSize);

            List<Block> blocks = this.blockRepository.GetBlocksAsync(blockHashes).GetAwaiter().GetResult();
            int availableBlocks = blocks.FindIndex(b => (b == null));
            if (availableBlocks < 0)
                availableBlocks = blocks.Count;

            this.logger.LogTrace("Available blocks are {0}", availableBlocks);

            if (availableBlocks > 0)
            {
                Block lastBlock = blocks[availableBlocks - 1];
                this.Put(blocks.GetRange(0, availableBlocks));
                this.logger.LogInformation("Synchronized {0} blocks with cross-chain store to advance tip to block {1}", availableBlocks, this.TipHashAndHeight.Height);
            }

            bool done = availableBlocks < synchronizationBatchSize;

            this.logger.LogTrace("(-):{0}", done);
            return done;
        }

        /// <inheritdoc />
        public Task<ICrossChainTransfer[]> GetAsync(uint256[] depositIds)
        {
            return Task.Run(() =>
            {
                this.Synchronize();

                ICrossChainTransfer[] res = this.ValidateCrossChainTransfers(this.Get(depositIds));

                return res;
            });
        }

        private ICrossChainTransfer[] Get(uint256[] depositIds)
        {
            using (CrossChainDBTransaction xdbTransaction = this.GetTransaction(CrossChainTransactionMode.Read))
            {
                return this.Get(xdbTransaction, depositIds);
            }
        }

        private OutPoint EarliestOutput(Transaction transaction)
        {
            Comparer<OutPoint> comparer = Comparer<OutPoint>.Create((x, y) => this.federationWalletManager.CompareOutpoints(x, y));
            return transaction.Inputs.Select(i => i.PrevOut).OrderByDescending(t => t, comparer).FirstOrDefault();
        }

        /// <inheritdoc />
        public Task<Dictionary<uint256, Transaction>> GetTransactionsByStatusAsync(CrossChainTransferStatus status, bool sort = false)
        {
            return Task.Run(() =>
            {
                lock (this.lockObj)
                {
                    this.Synchronize();

                    uint256[] partialTransferHashes = this.depositsIdsByStatus[status].ToArray();
                    ICrossChainTransfer[] partialTransfers = this.Get(partialTransferHashes).ToArray();

                    if (status == CrossChainTransferStatus.Partial || status == CrossChainTransferStatus.FullySigned)
                    {
                        this.ValidateCrossChainTransfers(partialTransfers);
                        partialTransfers = partialTransfers.Where(t => t.Status == status).ToArray();
                    }

                    Dictionary<uint256, Transaction> res;

                    if (sort)
                    {
                        res = partialTransfers
                            .Where(t => t.PartialTransaction != null)
                            .OrderBy(t => EarliestOutput(t.PartialTransaction), Comparer<OutPoint>.Create((x, y) => this.federationWalletManager.CompareOutpoints(x, y)))
                            .ToDictionary(t => t.DepositTransactionId, t => t.PartialTransaction);

                        this.logger.LogTrace("Returning {0} sorted results.", res.Count);
                    }
                    else
                    {
                        res = partialTransfers
                            .Where(t => t.PartialTransaction != null)
                            .ToDictionary(t => t.DepositTransactionId, t => t.PartialTransaction);

                        this.logger.LogTrace("Returning {0} results.", res.Count);
                    }

                    return res;
                }
            });
        }

        /// <summary>
        /// Forgets transfer information for the blocks being removed.
        /// </summary>
        /// <param name="xdbTransaction">The cross-chain db transaction context to use.</param>
        /// <param name="lastBlockHeight">The last block to retain.</param>
        private void OnDeleteBlocks(CrossChainDBTransaction xdbTransaction, int lastBlockHeight)
        {
            // Gather all the deposit ids that may have had transactions in the blocks being deleted.
            var depositIds = new HashSet<uint256>();
            uint256[] blocksToRemove = this.blockHeightsByBlockHash.Where(a => a.Value > lastBlockHeight).Select(a => a.Key).ToArray();

            foreach (HashSet<uint256> deposits in blocksToRemove.Select(a => this.depositIdsByBlockHash[a]))
            {
                depositIds.UnionWith(deposits);
            }

            // Find the transfers related to these deposit ids in the database.
            ICrossChainTransfer[] crossChainTransfers = this.Get(xdbTransaction, depositIds.ToArray());

            foreach (CrossChainTransfer transfer in crossChainTransfers)
            {
                // Transfers that only exist in the DB due to having been seen in a block should be removed completely.
                if (transfer.DepositHeight == null)
                {
                    // Delete the transfer completely.
                    xdbTransaction.DeleteTransfer(transfer);
                }
                else
                {
                    // Transaction is no longer seen.
                    transfer.SetStatus(CrossChainTransferStatus.FullySigned);

                    // Write the transfer status to the database.
                    xdbTransaction.PutTransfer(transfer);
                }
            }
        }

        public bool ValidateTransaction(Transaction transaction, bool checkSignature = false)
        {
            return this.federationWalletManager.ValidateTransaction(transaction, checkSignature);
        }

        /// <inheritdoc />
        public Dictionary<CrossChainTransferStatus, int> GetCrossChainTransferStatusCounter()
        {
            Dictionary<CrossChainTransferStatus, int> result = new Dictionary<CrossChainTransferStatus, int>();
            foreach (CrossChainTransferStatus status in Enum.GetValues(typeof(CrossChainTransferStatus)).Cast<CrossChainTransferStatus>())
            {
                result[status] = this.depositsIdsByStatus.TryGet(status)?.Count ?? 0;
            }

            return result;
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            this.SaveCurrentTipAsync().GetAwaiter().GetResult();
            this.cancellation.Cancel();
            base.Dispose();
        }
    }
}