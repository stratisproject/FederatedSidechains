using NBitcoin;
using Newtonsoft.Json;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    public class Withdrawal : IWithdrawal
    {
        public Withdrawal(uint256 depositId, uint256 id, Money amount, string targetAddress, int blockNumber, uint256 blockHash, uint blockTime)
        {
            this.DepositId = depositId;
            this.Id = id;
            this.Amount = amount;
            this.TargetAddress = targetAddress;
            this.BlockNumber = blockNumber;
            this.BlockHash = blockHash;
            this.BlockTime = blockTime;
        }

        /// <inheritdoc />
        public uint256 DepositId { get; }

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

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}