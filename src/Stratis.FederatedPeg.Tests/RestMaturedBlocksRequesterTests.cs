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
            this.crossChainTransferStore.NextMatureDepositHeight.Returns(1);

            this.httpClientFactory = Substitute.For<IHttpClientFactory>();

            bool called = false;
            this.httpClient = Substitute.For<HttpClient>();
            this.httpClient.PostAsync(Arg.Any<string>(), Arg.Any<HttpContent>())
              .Returns(Task.Run<HttpResponseMessage>(() => { called = true; return new HttpResponseMessage(); }));

            this.httpClientFactory.CreateClient(Arg.Any<string>()).Returns(this.httpClient);

            var restRequester = new RestMaturedBlockRequester(this.loggerFactory, this.federationSettings, this.httpClientFactory, this.asyncLoopFactory, this.crossChainTransferStore, this.nodeLifetime);
            restRequester.Start();

            while (!called)
                Thread.Sleep(100);

            Assert.True(called);
        }
    }
}
