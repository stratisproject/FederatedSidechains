using System;
using NBitcoin;

namespace Stratis.FederatedPeg.Features.FederationGateway.SourceChain
{
    /// <summary>
    /// The Monitor can use an Auditor to record all deposits and withdrawals that it
    /// receives from new blocks. This involves recording information for two transactions
    /// (source chain and target chain).
    /// </summary>
    public interface ITransferAuditor : IDisposable
    {
        /// <summary>
        /// Sets up the auditor.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Adds the initiating transaction info for a cross chain transaction.
        /// </summary>
        void AddTransferInfo(Transfer transferInfo);

        /// <summary>
        /// Loads the auditor data if required.
        /// </summary>
        void Load();

        /// <summary>
        /// Commits the audit to persistent storage.
        /// </summary>
        void Commit();

        /// <summary>
        /// Adds the identifier for the transaction on the counter chain.
        /// </summary>
        /// <param name="sourceTransactionId">The id of the source transaction (used as the id of the transfer too).</param>
        /// <param name="targetTransactionId">The id of the counter chain transaction.</param>
        void AddTargetTransactionId(uint256 sourceTransactionId, uint256 targetTransactionId);
    }
}
