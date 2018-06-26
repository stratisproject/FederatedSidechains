using System;
using NBitcoin;

namespace Stratis.FederatedPeg.Features.FederationGateway.MonitorChain
{
    public interface IMonitorChainSessionManager : IDisposable
    {
        void Initialize();

        MonitorChainSession CreateMonitorSession(int blockHeight);

        void CreateSessionOnCounterChain(int apiPort, MonitorChainSession monitorChainSession);
    }
}
