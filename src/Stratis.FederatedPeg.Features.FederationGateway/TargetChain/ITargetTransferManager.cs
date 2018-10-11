using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    /// <summary>
    /// The TargetTransferManager receives session data from the SourceChainSessionManager and takes a number of precautionary steps
    /// before signing the transaction. It also builds and broadcasts the transaction once a quorum of is received from the payload.
    /// </summary>
    public interface ITargetTransferManager
    {
       /// <summary>
        /// Do the work to process the transactions. In this method we start the process of requesting peer gateways to sign our transaction
        /// by building the partial transaction template and broadcasting it to peers.
        /// </summary>
        /// <param name="blockHeight">The height at which the transaction was initiated.</param>
        /// <returns></returns>
        Task<uint256> ProcessTransfers(int blockHeight);

        /// <summary>
        /// Receives a partial transaction inbound from the payload behavior.
        /// </summary>
        /// <param name="blockHeight">The session we are receiving.</param>
        /// <param name="partialTransaction">Inbound partial transaction.</param>
        /// <param name="bossCard">The insertion place in the partial transaction table we store in the session.</param>
        void ReceivePartial(int blockHeight, Transaction partialTransaction, uint256 bossCard);

        /// <summary>
        /// Called from our sister SourceChainSessionManager.  The monitor is reading our trusted chain and
        /// this registers the session data that tells us the trusted data we must use to verify we are signing
        /// a true transaction that has not been corrupted by a rouge or alien actor.
        /// </summary>
        void CreateTransferOnTargetChain(int blockHeight, List<TargetTransferRequest> counterChainTransactionInfos);

        /// <summary>
        /// VerifySession ensures it is safe to sign any inbound partial transaction by performing a
        /// number of checks against the session data. 
        /// </summary>
        /// <param name="sessionId">An id that identifies the session.</param>
        /// <param name="partialTransactionTemplate">The partial transaction we are asked to sign.</param>
        /// <returns></returns>
        CounterChainSession VerifySession(int blockHeight, Transaction partialTransactionTemplate);

        /// <summary>
        /// Record that we have signed the session.
        /// </summary>
        /// <param name="session">The session.</param>
        void MarkSessionAsSigned(CounterChainSession session);

        /// <summary>
        /// The counterchain transaction completed successfully and was identified on the blockchain by the monitor.
        /// Write the counterchain transationId back to the session.
        /// </summary>
        /// <param name="sessionId">The session that we will mark as completed.</param>
        /// <param name="transactionId">The transactionId of the compeleted transaction.</param>
        void AddCounterChainTransactionId(int blockHeight, uint256 transactionId);
    }
}
