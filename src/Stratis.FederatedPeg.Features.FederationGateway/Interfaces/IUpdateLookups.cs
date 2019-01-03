using System;
using System.Collections.Generic;
using NBitcoin;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface ICrossChainLookups
    {
        Dictionary<Type, IChangeTracker> CreateTrackers();
        void UpdateLookups(Dictionary<Type, IChangeTracker> trackers);
    }
}
