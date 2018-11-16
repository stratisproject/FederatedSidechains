using System;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface ILeaderReceiver : IDisposable
    {
        void ReceiveLeader(ILeaderProvider leaderProvider);

        IObservable<ILeaderProvider> LeaderProvidersStream { get; }
    }
}
