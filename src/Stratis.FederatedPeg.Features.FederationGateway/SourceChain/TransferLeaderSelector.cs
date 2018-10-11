using System;
using System.Collections.Generic;
using NBitcoin;
using Newtonsoft.Json;

namespace Stratis.FederatedPeg.Features.FederationGateway.SourceChain
{
    public class TransferLeaderSelector
    {
        public Transfer.Status Status { get; set; }

        private readonly DateTime startTime;

        public ICollection<Transfer> Transfers { get; set; }
        
        public int BlockNumber { get; }

        public BossTable BossTable { get; }

        // My boss card. I only get to build and broadcast the transaction when my boss card is in play.
        public string BossCard { get; }

        public uint256 TargetTransactionId { get; private set; } = uint256.Zero;

        /// <inheritdoc />
        public TransferLeaderSelector(int blockNumber, string[] federationPubKeys, string myPublicKey)
        {
            this.Status = Transfer.Status.Created;
            this.startTime = DateTime.Now;
            this.Transfers = new List<Transfer>();
            this.BlockNumber = blockNumber;

            this.BossTable = new BossTableBuilder().Build(blockNumber, federationPubKeys);
            this.BossCard = BossTable.MakeBossTableEntry(blockNumber, myPublicKey).ToString();
        }

        public void Complete(uint256 counterChainTransactionId)
        {
            this.Status = Transfer.Status.TargetMatured;
            this.TargetTransactionId = counterChainTransactionId;
        }

        public bool AmITheBoss(DateTime now) => this.BossTable.WhoHoldsTheBossCard(this.startTime, now) == this.BossCard;

        public string WhoHoldsTheBossCard(DateTime now) => this.BossTable.WhoHoldsTheBossCard(this.startTime, now);

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}