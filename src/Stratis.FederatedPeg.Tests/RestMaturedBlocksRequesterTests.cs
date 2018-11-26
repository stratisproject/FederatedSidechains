using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;
using Stratis.FederatedPeg.Features.FederationGateway.TargetChain;
using Stratis.FederatedPeg.Tests.Utils;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class RestMaturedBlocksRequesterTests
    {
        private IHttpClientFactory httpClientFactory;

        private HttpMessageHandler messageHandler;

        private HttpClient httpClient;

        private ILoggerFactory loggerFactory;

        private IFederationGatewaySettings federationSettings;

        private IAsyncLoopFactory asyncLoopFactory;

        private ICrossChainTransferStore crossChainTransferStore;

        private INodeLifetime nodeLifetime;

        private ILogger logger;

        public RestMaturedBlocksRequesterTests()
        {
            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.logger = Substitute.For<ILogger>();
            this.loggerFactory.CreateLogger(null).ReturnsForAnyArgs(this.logger);
            this.federationSettings = Substitute.For<IFederationGatewaySettings>();
            this.asyncLoopFactory = new AsyncLoopFactory(this.loggerFactory);
            this.nodeLifetime = new NodeLifetime();
            this.crossChainTransferStore = Substitute.For<ICrossChainTransferStore>();
        }

        [Fact]
        public void StartShouldCallGetMatureDeposits()
        {
            TestingHttpClient.PrepareWorkingHttpClient(ref this.messageHandler, ref this.httpClient, ref this.httpClientFactory);

            var maturedBlockDeposits = new MaturedBlockRequestModel(TestingValues.GetPositiveInt(), TestingValues.GetPositiveInt());

            var restRequester = new RestMaturedBlockRequester(this.loggerFactory, this.federationSettings, this.httpClientFactory, this.asyncLoopFactory, this.crossChainTransferStore, this.nodeLifetime);

            this.crossChainTransferStore.NextMatureDepositHeight.Returns(1);

            restRequester.Start();

            Thread.Sleep(100);

            this.logger.Received().Log(LogLevel.Debug,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString() == "Sending content Stratis.FederatedPeg.Features.FederationGateway.Models.JsonContent to Uri http://localhost:0/api/FederationGateway/get_matured_block_deposits"),
                null,
                Arg.Any<Func<object, Exception, string>>());
        }
    }
}
