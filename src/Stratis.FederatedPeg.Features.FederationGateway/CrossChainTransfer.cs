using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    /// <summary>
    /// Cross-chain transfer statuses.
    /// </summary>
    public enum CrossChainTransferStatus
    {
        TransactionCreated = 'T',
        FullySigned = 'F',
        SeenInBlock = 'S',
        Complete = 'C',
        Rejected = 'R'
    }

    /// <summary>
    /// Tracks the status of the cross-chain transfer.
    /// </summary>
    public class CrossChainTransfer : IBitcoinSerializable
    {
        /// <summary>
        /// The transaction id of the deposit transaction.
        /// </summary>
        private uint256 depositTransactionId;
        public uint256 DepositTransactionId => this.depositTransactionId;

        /// <summary>
        /// Indicated whether the deposit fields contain information.
        /// </summary>
        private bool depositPresent => this.depositTargetAddress != null;

        /// <summary>
        /// The block height of the deposit transaction.
        /// </summary>
        private long depositBlockHeight;

        /// <summary>
        /// The target address of the deposit transaction.
        /// </summary>
        private Script depositTargetAddress;

        /// <summary>
        /// The amount (in satoshis) of the deposit transaction.
        /// </summary>
        private long depositAmount;

        /// <summary>
        /// The unsigned partial transaction containing a full set of available UTXO's.
        /// </summary>
        private Transaction partialTransaction;

        /// <summary>
        /// The hash of the block where the transaction resides.
        /// </summary>
        private uint256 blockHash;

        /// <summary>
        /// The status of the cross chain transfer transaction.
        /// </summary>
        private CrossChainTransferStatus status;

        /// <summary>
        /// Parameter-less constructor for (de)serialization.
        /// </summary>
        public CrossChainTransfer()
        {
        }

        /// <summary>
        /// Constructs this object from passed parameters.
        /// </summary>
        /// <param name="status">The status of the cross chain transfer transaction.</param>
        /// <param name="depositTransactionId">The transaction id of the deposit transaction.</param>
        /// <param name="depositBlockHeight">The block height of the deposit transaction.</param>
        /// <param name="depositTargetAddress">The target address of the deposit transaction.</param>
        /// <param name="depositAmount">The amount (in satoshis) of the deposit transaction.</param>
        /// <param name="partialTransaction">The unsigned partial transaction containing a full set of available UTXO's.</param>
        /// <param name="blockHash">The hash of the block where the transaction resides.</param>
        public CrossChainTransfer(CrossChainTransferStatus status, uint256 depositTransactionId, long depositBlockHeight, Script depositTargetAddress, Money depositAmount, Transaction partialTransaction, uint256 blockHash)
        {
            this.status = status;
            this.depositTransactionId = depositTransactionId;
            this.depositBlockHeight = depositBlockHeight;
            this.depositTargetAddress = depositTargetAddress;
            this.depositAmount = depositAmount;
            this.partialTransaction = partialTransaction;
            this.blockHash = blockHash;
        }

        /// <summary>
        /// (De)serializes this object.
        /// </summary>
        /// <param name="stream">Stream to use for (de)serialization.</param>
        public void ReadWrite(BitcoinStream stream)
        {
            if (stream.Serializing)
            {
                Guard.Assert(this.IsValid());
            }

            byte status = (byte)this.status;
            stream.ReadWrite(ref status);
            this.status = (CrossChainTransferStatus)status;
            stream.ReadWrite(ref this.depositTransactionId);

            bool depositPresent = this.depositPresent;
            stream.ReadWrite(ref depositPresent);

            if (depositPresent)
            {
                stream.ReadWrite(ref this.depositBlockHeight);
                stream.ReadWrite(ref this.depositTargetAddress);
                stream.ReadWrite(ref this.depositAmount);
            }

            if (this.status == CrossChainTransferStatus.TransactionCreated || this.status == CrossChainTransferStatus.SeenInBlock || this.status == CrossChainTransferStatus.Complete)
            {
                stream.ReadWrite(ref this.partialTransaction);
                if (this.status != CrossChainTransferStatus.TransactionCreated)
                    stream.ReadWrite(ref this.blockHash);
            }
        }

        /// <summary>
        /// Depending on the status some fields can't be null.
        /// </summary>
        /// <returns><c>false</c> if the object is invalid and <c>true</c> otherwise.</returns>
        private bool IsValid()
        {
            if (this.status == CrossChainTransferStatus.TransactionCreated || this.status == CrossChainTransferStatus.SeenInBlock || this.status == CrossChainTransferStatus.Complete)
            {
                if (this.status != CrossChainTransferStatus.TransactionCreated)
                {
                    return this.blockHash != null;
                }

                return this.partialTransaction != null;
            }

            return this.depositTransactionId != null;
        }
    }
}
