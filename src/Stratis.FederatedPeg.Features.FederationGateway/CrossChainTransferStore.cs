using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DBreeze;
using DBreeze.DataTypes;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    /// <summary>
    /// Interface for interacting with the cross-chain transfer database.
    /// </summary>
    public interface ICrossChainTransferStore : IDisposable
    {
        /// <summary>
        /// Get the cross-chain transfer information from the database, identified by the deposit transaction id.
        /// </summary>
        /// <param name="depositTransactionId">The deposit transaction id.</param>
        /// <returns>The cross-chain transfer information.</returns>
        Task<CrossChainTransfer> GetAsync(uint256 depositTransactionId);

        /// <summary>
        /// Records the mature deposits at <see cref="NextMatureDepositHeight"/> on chain A.
        /// The value of <see cref="NextMatureDepositHeight"/> is incremented at the end of this call.
        /// </summary>
        /// <param name="crossChainTransfers">The deposit transactions.</param>
        /// <remarks>
        /// When building the list of transfers the caller should first use <see cref="GetAsync"/>
        /// to check whether the transfer already exists without the deposit information and
        /// then provide the updated object in this call.
        /// The caller must also ensure the transfers passed to this call all have a
        /// <see cref="CrossChainTransfer.status"/> of <see cref="CrossChainTransferStatus.TransactionCreated"/>.
        /// </remarks>
        Task RecordLatestMatureDeposits(IEnumerable<CrossChainTransfer> crossChainTransfers);

        /// <summary>
        /// Uses the information contained in a chain B block to update the store.
        /// Must be the block following ChainBTip (which will then also be advanced).
        /// Alternatively, if <paramref name="rewind"/> is set to <c>true</c>, this
        /// will undo the operation on the block at ChainBTip and move the tip back.
        /// </summary>
        /// <param name="block">The block following ChainBTip.</param>
        /// <param name="rewind">Set to <c>true</c> to undo the operation.</param>
        /// <remarks>
        /// The following statuses may be set:
        /// <list type="bullet">
        /// <item><see cref="CrossChainTransferStatus.SeenInBlock"/></item>
        /// <item><see cref="CrossChainTransferStatus.Complete"/></item>
        /// </list>
        /// </remarks>
        Task ProcessBlock(Block block, bool rewind);

        /// <summary>
        /// Updates partial transactions in the store with signatures obtained from the passed transactions.
        /// </summary>
        /// <param name="partialTransactions">Partial transactions received from other federation members.</param>
        /// <remarks>
        /// The following statuses may be set:
        /// <list type="bullet">
        /// <item><see cref="CrossChainTransferStatus.FullySigned"/></item>
        /// </list>
        /// </remarks>
        Task MergeTransactionSignatures(Transaction[] partialTransactions);

        /// <summary>
        /// Tip of chain B.
        /// </summary>
        ChainedHeader ChainBTip { get; }

        /// <summary>
        /// The block height on chain A for which the next list of deposits is expected.
        /// </summary>
        long NextMatureDepositHeight { get; }  // Chain A
    }

    public class CrossChainTransferStore : ICrossChainTransferStore
    {
        private const string TransferTableName = "Transfer";

        public long NextMatureDepositHeight { get; private set; }

        public ChainedHeader ChainBTip { get; private set; }

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Access to DBreeze database.</summary>
        private readonly DBreezeEngine DBreeze;

        private readonly Network network;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        public CrossChainTransferStore(Network network, DataFolder dataFolder, FederationGatewaySettings settings, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
            : this(network, Path.Combine(dataFolder.RootPath, settings.IsMainChain ? "mainchaindata" : "sidechaindata"), dateTimeProvider, loggerFactory)
        {
        }

        public CrossChainTransferStore(Network network, string folder, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotEmpty(folder, nameof(folder));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            Directory.CreateDirectory(folder);
            this.DBreeze = new DBreezeEngine(folder);
            this.network = network;
            this.dateTimeProvider = dateTimeProvider;
            this.NextMatureDepositHeight = 0;
            this.ChainBTip = null;
        }

        /// <summary>Performs any needed initialisation for the database.</summary>
        public virtual Task InitializeAsync()
        {
            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                // Currently don't do anything on startup

                this.logger.LogTrace("(-)");
            });

            return task;
        }

        /// <inheritdoc />
        public Task RecordLatestMatureDeposits(IEnumerable<CrossChainTransfer> crossChainTransfers)
        {
            throw new NotImplementedException("Not implemented yet");
        }

        /// <inheritdoc />
        public Task ProcessBlock(Block block, bool rewind)
        {
            throw new NotImplementedException("Not implemented yet");
        }

        /// <inheritdoc />
        public Task MergeTransactionSignatures(Transaction[] partialTransactions)
        {
            throw new NotImplementedException("Not implemented yet");
        }

        /// <inheritdoc />
        public Task<CrossChainTransfer> GetAsync(uint256 hash)
        {
            Guard.NotNull(hash, nameof(hash));

            Task<CrossChainTransfer> task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                CrossChainTransfer res = null;
                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;

                    Row<byte[], CrossChainTransfer> transferRow = transaction.Select<byte[], CrossChainTransfer>(TransferTableName, hash.ToBytes());

                    if (transferRow.Exists)
                    {
                        res = transferRow.Value;
                    }
                }

                this.logger.LogTrace("(-):{0}", res);
                return res;
            });

            return task;
        }

        /// <summary>
        /// Persist the cross-chain transfer information into the database.
        /// </summary>
        /// <param name="crossChainTransfer">Cross-chain transfer information to be inserted.</param>
        private Task PutAsync(CrossChainTransfer crossChainTransfer)
        {
            Guard.NotNull(crossChainTransfer, nameof(crossChainTransfer));

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    this.OnInsertCrossChainTransfer(transaction, crossChainTransfer);

                    transaction.Commit();
                }

                this.logger.LogTrace("(-)");
            });

            return task;
        }

        /// <summary>
        /// Determine if cross-chain transfer already exists in the database.
        /// </summary>
        /// <param name="depositTransactionId">The deposit transaction id.</param>
        /// <returns><c>true</c> if the transfer can be found in the database, otherwise return <c>false</c>.</returns>
        private Task<bool> ExistAsync(uint256 depositTransactionId)
        {
            Guard.NotNull(depositTransactionId, nameof(depositTransactionId));

            Task<bool> task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                bool res = false;
                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    // Lazy loading is on so we don't fetch the whole value, just the row.
                    Row<byte[], CrossChainTransfer> transferRow = transaction.Select<byte[], CrossChainTransfer>(TransferTableName, depositTransactionId.ToBytes());

                    if (transferRow.Exists)
                    {
                        res = true;
                    }
                }

                this.logger.LogTrace("(-):{0}", res);
                return res;
            });

            return task;
        }

        /// <summary>
        /// Deletes the cross-chain transfer information from the database.
        /// </summary>
        /// <param name="depositTransactionId">The deposit transaction id.</param>
        public Task DeleteAsync(uint256 depositTransactionId)
        {
            Guard.NotNull(depositTransactionId, nameof(depositTransactionId));

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;

                    this.OnDeleteCrossChainTransfer(transaction, depositTransactionId);

                    transaction.Commit();
                }

                this.logger.LogTrace("(-)");
            });

            return task;
        }

        protected virtual void OnInsertCrossChainTransfer(DBreeze.Transactions.Transaction dbreezeTransaction, CrossChainTransfer crossChainTransfer)
        {
            // If the transfer is already in store don't write it again.
            Row<byte[], CrossChainTransfer> transferRow = dbreezeTransaction.Select<byte[], CrossChainTransfer>(TransferTableName, crossChainTransfer.DepositTransactionId.ToBytes());

            if (!transferRow.Exists)
            {
                dbreezeTransaction.Insert<byte[], CrossChainTransfer>(TransferTableName, crossChainTransfer.DepositTransactionId.ToBytes(), crossChainTransfer);
            }
        }

        protected virtual void OnDeleteCrossChainTransfer(DBreeze.Transactions.Transaction dbreezeTransaction, uint256 hash)
        {
            dbreezeTransaction.RemoveKey<byte[]>(TransferTableName, hash.ToBytes());
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.DBreeze.Dispose();
        }
    }
}
