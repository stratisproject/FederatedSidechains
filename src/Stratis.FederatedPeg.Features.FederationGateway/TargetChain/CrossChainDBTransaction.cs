using System;
using System.Collections.Generic;
using DBreeze;
using DBreeze.DataTypes;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    public enum CrossChainTransactionMode
    {
        Read,
        ReadWrite
    }

    public class CrossChainDBTransaction : IDisposable
    {
        private readonly DBreezeSerializer dBreezeSerializer;
        private DBreeze.Transactions.Transaction transaction;
        private readonly ICrossChainLookups crossChainLookups;
        private readonly CrossChainTransactionMode mode;
        private StatusChangeTracker tracker;

        private CrossChainDBTransaction(DBreeze.Transactions.Transaction transaction, DBreezeSerializer dbreezeSerializer, ICrossChainLookups updateLookups, CrossChainTransactionMode mode)
        {
            this.transaction = transaction;
            this.dBreezeSerializer = dbreezeSerializer;
            this.crossChainLookups = updateLookups;
            this.mode = mode;

            transaction.ValuesLazyLoadingIsOn = false;

            if (mode == CrossChainTransactionMode.ReadWrite)
            {
                transaction.SynchronizeTables(CrossChainDB.TransferTableName, CrossChainDB.CommonTableName);
            }

            this.tracker = new StatusChangeTracker();
        }

        public static CrossChainDBTransaction GetTransaction(DBreezeEngine dBreezeEngine, DBreezeSerializer dbreezeSerializer, ICrossChainLookups updateLookups, CrossChainTransactionMode mode)
        {
            return new CrossChainDBTransaction(dBreezeEngine.GetTransaction(eTransactionTablesLockTypes.EXCLUSIVE), dbreezeSerializer, updateLookups, mode);
        }

        public ICrossChainTransfer GetTransfer(uint256 depositId)
        {
            Row<byte[], byte[]> transferRow = this.transaction.Select<byte[], byte[]>(CrossChainDB.TransferTableName, depositId.ToBytes());

            if (transferRow.Exists)
            {
                // Workaround for shortcoming in DBreeze serialization.
                var crossChainTransfer = new CrossChainTransfer();
                crossChainTransfer.FromBytes(transferRow.Value, this.dBreezeSerializer.Network.Consensus.ConsensusFactory);
                crossChainTransfer.RecordDbStatus();

                return crossChainTransfer;
            }

            return null;
        }

        public IEnumerable<ICrossChainTransfer> EnumerateTransfers()
        {
            foreach (Row<byte[], byte[]> transferRow in this.transaction.SelectForward<byte[], byte[]>(CrossChainDB.TransferTableName))
            {
                // Workaround for shortcoming in DBreeze serialization.
                var crossChainTransfer = new CrossChainTransfer();
                crossChainTransfer.FromBytes(transferRow.Value, this.dBreezeSerializer.Network.Consensus.ConsensusFactory);
                crossChainTransfer.RecordDbStatus();

                yield return crossChainTransfer;
            }
        }

        public void PutTransfer(ICrossChainTransfer transfer)
        {
            Guard.Assert(this.mode != CrossChainTransactionMode.Read);

            // Record the old status
            this.tracker[transfer] = transfer.DbStatus;

            // Write the transfer.
            this.transaction.Insert(CrossChainDB.TransferTableName, transfer.DepositTransactionId.ToBytes(), transfer);
        }

        public void DeleteTransfer(ICrossChainTransfer transfer)
        {
            Guard.NotNull(transfer, nameof(transfer));

            // Only transfers that exist in the db purely due to being seen in a block will be removed.
            Guard.Assert(transfer.DepositHeight == null);

            this.tracker[transfer] = transfer.DbStatus;

            this.transaction.RemoveKey<byte[]>(CrossChainDB.TransferTableName, transfer.DepositTransactionId.ToBytes());
        }

        public void Commit()
        {
            Guard.Assert(this.mode != CrossChainTransactionMode.Read);

            this.transaction.Commit();
            this.crossChainLookups.UpdateLookups(this.tracker);
        }

        public void Rollback()
        {
            Guard.Assert(this.mode != CrossChainTransactionMode.Read);

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
            return string.Format("{0}({1})", this.transaction.CreatedUdt.GetHashCode(), this.transaction.ManagedThreadId);
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
