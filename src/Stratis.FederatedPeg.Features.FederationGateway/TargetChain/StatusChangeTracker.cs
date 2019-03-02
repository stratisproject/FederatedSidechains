using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    /// <summary>
    /// Tracks changed transfers and records their original status.
    /// </summary>
    public class StatusChangeTracker : Dictionary<ICrossChainTransfer, CrossChainTransferStatus?>, IChangeTracker
    {
        /// <summary>
        /// Records the status that was originally read from the database.
        /// </summary>
        /// <param name="transfer">The transfer to record the original status of.</param>
        public void RecordOldValue(IBitcoinSerializable transfer)
        {
            this[(ICrossChainTransfer)transfer] = ((ICrossChainTransfer)transfer).DbStatus;
        }

        /// <summary>
        /// Instructs the transfer to record its (original) status. Typically called after reading it from the database.
        /// </summary>
        /// <param name="transfer">The transfer that should record its status.</param>
        public void SetOldValue(IBitcoinSerializable transfer)
        {
            ((ICrossChainTransfer)transfer).RecordDbStatus();
        }

        /// <summary>
        /// This is used by standalone (not created in <see cref="CrossChainDBTransaction"/> context) trackers
        /// to set the status on a transfer and at the same time note the change in the tracker.
        /// </summary>
        /// <param name="transfer">The cross-chain transfer to update.</param>
        /// <param name="status">The new status.</param>
        /// <param name="blockHash">The block hash of the partialTranction.</param>
        /// <param name="blockHeight">The block height of the partialTransaction.</param>
        /// <remarks>
        /// Within the store the earliest status is <see cref="CrossChainTransferStatus.Partial"/>. In this case <c>null</c>
        /// is used to flag a new transfer within the tracker only. It means that there is no earlier status.
        /// </remarks>
        public void SetTransferStatus(ICrossChainTransfer transfer, CrossChainTransferStatus? status = null, uint256 blockHash = null, int blockHeight = 0)
        {
            if (status != null)
            {
                // If setting the status then record the previous status.
                this[transfer] = transfer.Status;
                transfer.SetStatus((CrossChainTransferStatus)status, blockHash, blockHeight);
            }
            else
            {
                // This is a new object and there is no previous status.
                this[transfer] = null;
            }
        }

        /// <summary>
        /// Returns a list of unique block hashes for the transfers being tracked.
        /// </summary>
        /// <returns>A list of unique block hashes for the transfers being tracked.</returns>
        public uint256[] UniqueBlockHashes()
        {
            // This tests for transfers containing block hashes and checks that this is not a deletion.
            return this.Keys.Where(k => k.BlockHash != null && (k.DepositHeight != null || k.DbStatus == null)).Select(k => k.BlockHash).Distinct().ToArray();
        }
    }
}
