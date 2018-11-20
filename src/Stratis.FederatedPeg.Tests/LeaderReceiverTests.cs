using System;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.TargetChain;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class LeaderReceiverTests : IDisposable
    {
        private ILeaderReceiver leaderReceiver;
        private IDisposable streamSubscription;
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger logger;
        private readonly IFederationGatewaySettings federationGatewaySettings;

        public LeaderReceiverTests()
        {
            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.logger = Substitute.For<ILogger>();
            this.loggerFactory.CreateLogger(null).ReturnsForAnyArgs(this.logger);
            this.federationGatewaySettings = Substitute.For<IFederationGatewaySettings>();
        }

        [Fact]
        public void ReceiveLeaders_Should_Push_Items_Into_The_LeaderProvidersStream()
        {
            this.leaderReceiver = new LeaderReceiver(this.loggerFactory);

            const int LeaderCount = 3;
            var receivedLeaderCount = 0;
            
            this.streamSubscription = this.leaderReceiver.LeaderProvidersStream.Subscribe(
                _ => { receivedLeaderCount++; });

            for (var i = 0; i < LeaderCount; i++)
                this.leaderReceiver.ReceiveLeader(new LeaderProvider(this.federationGatewaySettings));

            receivedLeaderCount.Should().Be(LeaderCount);

            var logMsg = string.Format("Received new leaderProvider for" + Environment.NewLine +
                "System.Reactive.Subjects.ReplaySubject`1[Stratis.FederatedPeg.Features.FederationGateway.Interfaces.ILeaderProvider]");

            this.logger.Received(receivedLeaderCount).Log(LogLevel.Debug, 
                Arg.Any<EventId>(), 
                Arg.Is<object>(o => o.ToString() == logMsg),
                null,
                Arg.Any<Func<object, Exception, string>>());
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.streamSubscription?.Dispose();
            this.leaderReceiver?.Dispose();
        }
    }
}
