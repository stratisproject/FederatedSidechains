using NBitcoin;
using Newtonsoft.Json;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.SourceChain
{
    public class Deposit : IDeposit
    {
        public Deposit(uint256 id, Money amount, string targetAddress, int blockNumber, uint256 blockHash, uint blockTime)
        {
            this.Id = id;
            this.Amount = amount;
            this.TargetAddress = targetAddress;
            this.BlockNumber = blockNumber;
            this.BlockHash = blockHash;
            this.BlockTime = blockTime;
        }

        /// <inheritdoc />
        public uint256 Id { get; }

        /// <inheritdoc />
        public Money Amount { get; }

        /// <inheritdoc />
        public string TargetAddress { get; }

        /// <inheritdoc />
        public int BlockNumber { get; }

        /// <inheritdoc />
        public uint256 BlockHash { get; }

        /// <inheritdoc />
        public uint BlockTime { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}