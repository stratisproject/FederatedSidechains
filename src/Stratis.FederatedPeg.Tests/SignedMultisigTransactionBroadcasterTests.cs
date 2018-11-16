using System;
using System.Linq;
using System.Reactive.Linq;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.TargetChain;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class SignedMultisigTransactionBroadcasterTests : IDisposable
    {
        private readonly ILeaderReceiver leaderReceiver;

        private readonly ILoggerFactory loggerFactory;

        private IDisposable streamSubscription;

        private IFederationGatewaySettings federationGatewaySettings;

        public SignedMultisigTransactionBroadcasterTests()
        {
            this.leaderReceiver = new LeaderReceiver(this.loggerFactory);
            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.federationGatewaySettings = Substitute.For<IFederationGatewaySettings>();
        }

        [Fact]
        public void TODO()
        {
            // TODO : Similar test in EventsPersisterTests

            var leaderProvider = new LeaderProvider(this.federationGatewaySettings);

            IObservable<ILeaderProvider> leaderProvidersStream = new[] { leaderProvider }.ToObservable();

            this.leaderReceiver.LeaderProvidersStream.Returns(leaderProvidersStream);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.streamSubscription?.Dispose();
            this.leaderReceiver?.Dispose();
        }
    }
}
