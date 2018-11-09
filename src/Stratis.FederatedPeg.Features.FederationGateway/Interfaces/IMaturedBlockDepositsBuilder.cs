using NBitcoin;
using Stratis.Bitcoin.Primitives;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface IMaturedBlockDepositsProcessor
    {
        MaturedBlockDepositsModel ExtractMaturedBlockDeposits(ChainedHeaderBlock latestPublishedBlock);
    }
}
