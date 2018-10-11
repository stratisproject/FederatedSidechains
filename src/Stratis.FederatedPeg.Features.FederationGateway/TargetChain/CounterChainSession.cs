using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.FederatedPeg.Features.FederationGateway.SourceChain;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    public class CounterChainSession
    {
        public List<Transaction> PartialTransactions { get; }
        public int BlockHeight { get; }
        public bool HasReachedQuorum => this.PartialTransactions.Count >= this.FederationGatewaySettings.MultiSigM;
        public bool HaveISigned { get; set; } = false;
        public FederationGatewaySettings FederationGatewaySettings { get; }

        public IList<Transfer> Transfers { get; set; }

        private readonly ILogger logger;

        // The transactionId of the completed transaction.
        public uint256 TargetTransactionId { get; internal set; } = uint256.Zero;

        public CounterChainSession(ILogger logger, FederationGatewaySettings federationGatewaySettings, int blockHeight)
        {
            this.logger = logger;
            this.FederationGatewaySettings = federationGatewaySettings;
            this.Transfers = new List<Transfer>();
            this.PartialTransactions = new List<Transaction>();
            this.BlockHeight = blockHeight;
        }

        internal bool AddPartial(Transaction partialTransaction, string bossCard)
        {
            this.logger.LogTrace("()");
            if (partialTransaction == null)
            {
                this.logger.LogDebug("Skipped adding a null partial transaction");
                return false;
            }

            // Insert the partial transaction in the session if has not been added yet.
            if (!this.PartialTransactions.Any(pt => pt.GetHash() == partialTransaction.GetHash() && pt.Inputs.First().ScriptSig == partialTransaction.Inputs.First().ScriptSig))
            {
                this.logger.LogDebug("Adding Partial to CounterChainSession.");
                this.PartialTransactions.Add(partialTransaction);
            }
            else
            {
                this.logger.LogDebug("Partial already added to CounterChainSession.");
            }

            // Output parts info.
            this.logger.LogDebug("List of partials transactions");
            this.logger.LogDebug(" ---------");
            foreach (var p in this.PartialTransactions)
            {
                this.logger.LogDebug(p.ToHex());
            }

            // Have we reached Quorum?
            this.logger.LogDebug("---------");
            this.logger.LogDebug(string.Format("HasQuorum: {0}", this.HasReachedQuorum));
            this.logger.LogTrace("(-)");

            // End output. 
            return this.HasReachedQuorum;
        }
    }
}