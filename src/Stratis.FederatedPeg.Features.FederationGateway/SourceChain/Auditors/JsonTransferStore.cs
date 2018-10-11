using System.Collections.Concurrent;
using System.Linq;
using NBitcoin;

namespace Stratis.FederatedPeg.Features.FederationGateway.SourceChain.Auditors
{
    /// <summary>
    /// A store used to hold information about cross chain transactions. This store is also persisted to disk. 
    /// </summary>
    internal sealed class JsonTransferStore
    {
        /// <summary>
        /// Returns the count of items in the store.
        /// </summary>
        public int Count => this.Transfers.Count;

        public JsonTransferStore()
        {
            this.Transfers = new ConcurrentBag<Transfer>();
        }

        // In memory structure used for the storage of cross chain transaction infos.
        public ConcurrentBag<Transfer> Transfers { get; }

        public void Add(Transfer transfer)
        {
            this.Transfers.Add(transfer);
        }

        // todo: convert to a dictionary
        // todo: include proper exception handing
        public void AddCrossChainTransactionId(uint256 sessionId, uint256 crossChainTransactionId)
        {
            var crossChainTransactionInfo = this.Transfers.FirstOrDefault(c => c.SourceTransactionId == sessionId);
            if (crossChainTransactionInfo != null)
            {
                crossChainTransactionInfo.SourceTransactionId = crossChainTransactionId;
            }
        }
    }
}