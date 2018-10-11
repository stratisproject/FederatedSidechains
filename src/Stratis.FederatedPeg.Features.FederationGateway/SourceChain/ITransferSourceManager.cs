using System;

namespace Stratis.FederatedPeg.Features.FederationGateway.SourceChain
{
    public interface ITransferSourceManager : IDisposable
    {
        void Initialize();

        void Register(TransferLeaderSelector monitorSession);

        void CreateSessionOnCounterChain(int apiPort, TransferLeaderSelector monitorChainSession);
    }
}
