using System;
using System.Collections.Generic;
using DBreeze.DataTypes;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    /// <summary>Supported DBreeze transaction modes.</summary>
    public enum CrossChainTransactionMode
    {
        Read,
        ReadWrite
    }

    /// <summary>
    /// The purpose of this class is to restrict the operations that can be performed on the underlying
    /// database - i.e. it provides a "higher level" layer to the underlying DBreeze transaction.
    /// As such it provides a guarantees that any transient lookups will be kept in step with changes
    /// to the database. It also handles all required serialization here in one place.
    /// </summary>
    public class CrossChainDBTransaction : IDisposable
    {
        /// <summary>The serializer to use for this transaction.</summary>
        private readonly DBreezeSerializer dBreezeSerializer;

        /// <summary>The underlying DBreeze transaction.</summary>
        private DBreeze.Transactions.Transaction transaction;

        /// <summary>Interface providing control over the updating of transient lookups.</summary>
        private readonly ICrossChainLookups crossChainLookups;

        /// <summary>The mode of the transaction.</summary>
        private readonly CrossChainTransactionMode mode;

        /// <summary>Tracking changes allows updating of transient lookups after a successful commit operation.</summary>
        private Dictionary<Type, IChangeTracker> trackers;

        /// <summary>
        /// Constructs a transaction object that acts as a wrapper around the database tables.
        /// </summary>
        /// <param name="dbreeze">The DBreeze database engine.</param>
        /// <param name="dbreezeSerializer">The DBreeze serializer to use.</param>
        /// <param name="updateLookups">Interface providing methods to orchestrate the updating of transient lookups.</param>
        /// <param name="mode">The mode in which to interact with the database.</param>
        public CrossChainDBTransaction(
            DBreeze.DBreezeEngine dbreeze,
            DBreezeSerializer dbreezeSerializer,
            ICrossChainLookups updateLookups,
            CrossChainTransactionMode mode)
        {
            this.transaction = dbreeze.GetTransaction();
            this.dBreezeSerializer = dbreezeSerializer;
            this.crossChainLookups = updateLookups;
            this.mode = mode;

            this.transaction.ValuesLazyLoadingIsOn = false;

            if (mode == CrossChainTransactionMode.ReadWrite)
            {
                this.transaction.SynchronizeTables(CrossChainDB.TransferTableName, CrossChainDB.CommonTableName);
            }

            this.trackers = updateLookups.CreateTrackers();
        }

        private void Insert<TKey, TObject>(string tableName, TKey key, TObject obj) where TObject : IBitcoinSerializable
        {
            Guard.Assert(this.mode == CrossChainTransactionMode.ReadWrite);

            byte[] keyBytes = this.dBreezeSerializer.Serialize(key);
            byte[] objBytes = this.dBreezeSerializer.Serialize(obj);
            this.transaction.Insert(tableName, keyBytes, objBytes);

            // If this is a tracked class.
            if (this.trackers.TryGetValue(typeof(TObject), out IChangeTracker tracker))
            {
                // Record the object and its old value.
                tracker.RecordOldValue(obj);
            }
        }

        private bool Select<TKey, TObject>(string tableName, TKey key, out TObject obj) where TObject : IBitcoinSerializable
        {
            byte[] keyBytes = this.dBreezeSerializer.Serialize(key);
            Row<byte[], byte[]> row = this.transaction.Select<byte[], byte[]>(tableName, keyBytes);

            if (!row.Exists)
            {
                obj = default(TObject);
                return false;
            }

            obj = this.dBreezeSerializer.Deserialize<TObject>(row.Value);

            // If this is a tracked class.
            if (this.trackers.TryGetValue(typeof(TObject), out IChangeTracker tracker))
            {
                // Set the old value on the object itself so that we can update the lookups if it is changed.
                tracker.SetOldValue(obj);
            }

            return true;
        }

        private IEnumerable<TObject> SelectForward<TKey, TObject>(string tableName) where TObject : IBitcoinSerializable
        {
            if (!this.trackers.TryGetValue(typeof(TObject), out IChangeTracker tracker))
                tracker = null;

            foreach (Row<byte[], byte[]> row in this.transaction.SelectForward<byte[], byte[]>(tableName))
            {
                TObject obj = this.dBreezeSerializer.Deserialize<TObject>(row.Value);

                // If this is a tracked class.
                if (tracker != null)
                {
                    // Set the old value on the object itself so that we can update the lookups if it is changed.
                    tracker.SetOldValue(obj);
                }

                yield return obj;
            }
        }

        private void RemoveKey<TKey, TObject>(string tableName, TKey key, TObject obj) where TObject : IBitcoinSerializable
        {
            Guard.Assert(this.mode == CrossChainTransactionMode.ReadWrite);

            byte[] keyBytes = this.dBreezeSerializer.Serialize(key);
            this.transaction.RemoveKey(tableName, keyBytes);

            // If this is a tracked class.
            if (!this.trackers.TryGetValue(typeof(TObject), out IChangeTracker tracker))
            {
                // Record the object and its old value.
                tracker.RecordOldValue(obj);
            }
        }

        public ICrossChainTransfer GetTransfer(uint256 depositId)
        {
            if (!Select(CrossChainDB.TransferTableName, depositId, out CrossChainTransfer crossChainTransfer))
                return null;

            return crossChainTransfer;
        }

        public IEnumerable<ICrossChainTransfer> EnumerateTransfers()
        {
            foreach (ICrossChainTransfer crossChainTransfer in SelectForward<uint256, CrossChainTransfer>(CrossChainDB.TransferTableName))
                yield return crossChainTransfer;
        }

        public void PutTransfer(ICrossChainTransfer transfer)
        {
            Guard.NotNull(transfer, nameof(transfer));

            // Write the transfer.
            Insert(CrossChainDB.TransferTableName, transfer.DepositTransactionId, transfer);
        }

        public void DeleteTransfer(ICrossChainTransfer transfer)
        {
            Guard.NotNull(transfer, nameof(transfer));

            // Only transfers that exist in the db solely due to being seen in a block will be removed.
            Guard.Assert(transfer.DepositHeight == null);

            RemoveKey(CrossChainDB.TransferTableName, transfer.DepositTransactionId, transfer);
        }

        public void Commit()
        {
            Guard.Assert(this.mode == CrossChainTransactionMode.ReadWrite);

            this.transaction.Commit();
            this.crossChainLookups.UpdateLookups(this.trackers);
        }

        public void Rollback()
        {
            Guard.Assert(this.mode == CrossChainTransactionMode.ReadWrite);

            this.transaction.Rollback();
        }

        public BlockLocator LoadTipHashAndHeight()
        {
            var blockLocator = new BlockLocator();
            try
            {
                Row<byte[], byte[]> row = this.transaction.Select<byte[], byte[]>(CrossChainDB.CommonTableName, CrossChainDB.RepositoryTipKey);
                Guard.Assert(row.Exists);
                blockLocator.FromBytes(row.Value);
            }
            catch (Exception)
            {
                blockLocator.Blocks = new List<uint256> { this.dBreezeSerializer.Network.GenesisHash };
            }

            return blockLocator;
        }

        public void SaveTipHashAndHeight(BlockLocator blockLocator)
        {
            Guard.Assert(this.mode != CrossChainTransactionMode.Read);

            this.transaction.Insert<byte[], byte[]>(CrossChainDB.CommonTableName, CrossChainDB.RepositoryTipKey, blockLocator.ToBytes());
        }

        public int? LoadNextMatureHeight()
        {
            Row<byte[], int> row = this.transaction.Select<byte[], int>(CrossChainDB.CommonTableName, CrossChainDB.NextMatureTipKey);

            return (row?.Exists ?? false) ? row.Value : (int?)null;
        }

        public void SaveNextMatureHeight(int newTip)
        {
            Guard.Assert(this.mode != CrossChainTransactionMode.Read);

            this.transaction.Insert<byte[], int>(CrossChainDB.CommonTableName, CrossChainDB.NextMatureTipKey, newTip);
        }

        /// <summary>A string to identify this transaction by.</summary>
        /// <returns>A concatenation of the creation time and thread id.</returns>
        public override string ToString()
        {
            DateTime createdDT = new DateTime(this.transaction.CreatedUdt);
            return string.Format("{0}:{1}", createdDT, this.transaction.ManagedThreadId);
        }

        public void Dispose()
        {
            if (this.transaction != null)
            {
                this.transaction.Dispose();
                this.transaction = null;
            }
        }
    }
}
