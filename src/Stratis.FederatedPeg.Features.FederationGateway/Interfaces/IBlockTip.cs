using Stratis.Bitcoin.Utilities;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface IBlockTip
    {
        string Hash { get; }

        int Height { get; }
    }
}