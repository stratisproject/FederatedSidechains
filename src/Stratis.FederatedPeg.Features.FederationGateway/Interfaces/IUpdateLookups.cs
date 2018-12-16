using Stratis.FederatedPeg.Features.FederationGateway.TargetChain;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface ICrossChainLookups
    {
        void UpdateLookups(StatusChangeTracker tracker);
    }
}
