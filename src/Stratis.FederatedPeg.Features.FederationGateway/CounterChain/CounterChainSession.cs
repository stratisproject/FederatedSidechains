using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.FederatedPeg.Features.FederationGateway;

public class CounterChainSession
    {
        private readonly Transaction[] partialTransactions;

        public uint256 SessionId { get; }

        public Money Amount { get; }

        public string Destination { get; }
    
        // todo: we can remove this if we just use a list for the partials
        private readonly BossTable bossTable;

        public bool HasReachedQuorum { get; private set; }

        public Transaction[] PartialTransactions => this.partialTransactions;

        private readonly ILogger logger;

        public bool HaveISigned { get; set; } = false;
        public FederationGatewaySettings federationGatewaySettings { get; }


        public CounterChainSession(ILogger logger,
            FederationGatewaySettings federationGatewaySettings,
            uint256 sessionId,
            Money amount, 
            string destination)
        {
            this.logger = logger;
            this.federationGatewaySettings = federationGatewaySettings;
            this.partialTransactions = new Transaction[federationGatewaySettings.MultiSigN];
            this.SessionId = sessionId;
            this.Amount = amount;
            this.Destination = destination;
            this.bossTable = new BossTableBuilder()
                .Build(sessionId, 
                    federationGatewaySettings.FederationPublicKeys.Select(k => k.ToString()));
        }

        internal bool AddPartial(Transaction partialTransaction, string bossCard)
        {
            this.logger.LogTrace("()");
            this.logger.LogInformation("Adding Partial to CounterChainSession.");
            
            // Insert the partial transaction in the session.
            var positionInTable = bossTable.BossTableEntries.IndexOf(bossCard);
            this.partialTransactions[positionInTable] = partialTransaction;
            
            // Output parts info.
            this.logger.LogInformation("New Partials");
            this.logger.LogInformation(" ---------");
            foreach (var p in partialTransactions)
            {
                this.logger.LogInformation(p == null ? "null" : $"{p?.ToHex()}");
            }
                
            // Have we reached Quorum?
            this.HasReachedQuorum = this.CountPartials() >= federationGatewaySettings.MultiSigM;
            this.logger.LogInformation($"---------");
            this.logger.LogInformation($"HasQuorum: {this.HasReachedQuorum}");
            this.logger.LogTrace("(-)");
            
            // End output. 
            return this.HasReachedQuorum;
        }

        private int CountPartials()
        {
            return partialTransactions.Count(t => t != null);
        }
    }