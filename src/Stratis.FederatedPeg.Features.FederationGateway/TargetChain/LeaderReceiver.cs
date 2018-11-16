using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    public class LeaderReceiver : ILeaderReceiver, IDisposable
    {
        private readonly ReplaySubject<ILeaderProvider> leaderProvidersStream;

        private readonly ILogger logger;

        public LeaderReceiver(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.leaderProvidersStream = new ReplaySubject<ILeaderProvider>(1);
            this.LeaderProvidersStream = this.leaderProvidersStream.AsObservable();
        }

        public IObservable<ILeaderProvider> LeaderProvidersStream { get; }

        public void ReceiveLeader(ILeaderProvider leaderProvider)
        {
            this.logger.LogDebug("Received new leaderProvider for{0}{1}", Environment.NewLine, this.leaderProvidersStream);
            this.leaderProvidersStream.OnNext(leaderProvider);
        }


        public void Dispose()
        {
            this.leaderProvidersStream?.Dispose();
        }
    }
}
