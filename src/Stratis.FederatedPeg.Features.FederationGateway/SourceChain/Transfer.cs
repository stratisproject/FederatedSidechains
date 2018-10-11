using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.FederatedPeg.Features.FederationGateway.SourceChain
{
    public class Transfer
    {
        /// <summary>
        /// The Id of the source transaction that originates the fund transfer.
        /// </summary>
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 SourceTransactionId { get; set; }

        /// <summary>
        /// The amount deposited on the source chain multisig.
        /// </summary>
        public Money SourceDepositAmount { get; set; }

        /// <summary>
        /// The block number where the source transaction resides.
        /// </summary>
        public int SourceBlockNumber { get; set; }

        /// <summary>
        /// The hash of the block where source the transaction resides.
        /// </summary>
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 SourceBlockHash { get; set; }

        /// <summary>
        /// The hash of the transaction that moved the funds out of the multisig on the target chain.
        /// </summary>
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 TargetTransactionId { get; set; }

        /// <summary>
        /// The final destination of funds (on the target chain).
        /// </summary>
        public string TargetAddress { get; set; }

        /// <summary>
        /// The block number where the target transaction resides.
        /// </summary>
        public int TargetBlockNumber { get; set; }

        /// <summary>
        /// The hash of the block where target the transaction resides.
        /// </summary>
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 TargetBlockHash { get; set; }

        /// <summary>
        /// The current status of the cross chain transfer.
        /// </summary>
        public Status CurrentStatus { get; set; }

        /// <summary>
        /// Helper to generate a json representation of this structure for logging/debugging.
        /// </summary>
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public class Status
        {
            private string name;

            private Status(string name) { this.name = name; }

            public override string ToString() => this.name;

            public static Status SourceMatured = new Status(nameof(SourceMatured));
            public static Status Created = new Status(nameof(Created));
            public static Status PendingSignatures = new Status(nameof(PendingSignatures));
            public static Status Signed = new Status(nameof(Signed));
            public static Status InMempool = new Status(nameof(InMempool));
            public static Status OnChain = new Status(nameof(OnChain));
            public static Status TargetMatured = new Status(nameof(TargetMatured));

            public static Status Rejected = new Status(nameof(Rejected));

            public bool RequiresResend()
            {
                return this != InMempool
                       && this != OnChain
                       && this != TargetMatured
                       && this != Rejected;
            }
        }
    }
}