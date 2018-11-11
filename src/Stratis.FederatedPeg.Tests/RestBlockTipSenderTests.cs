using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.FederatedPeg.Features.FederationGateway.Models;
using Stratis.FederatedPeg.Features.FederationGateway.SourceChain;
using Stratis.FederatedPeg.Tests.Utils;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class RestBlockTipSenderTests : IDisposable
    {
        private IHttpClientFactory httpClientFactory;

        private HttpMessageHandler messageHandler;

        private HttpClient httpClient;

        private ILoggerFactory loggerFactory;

        private IFederationGatewaySettings federationSettings;

        private ILogger logger;

        public RestBlockTipSenderTests()
        {
            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.logger = Substitute.For<ILogger>();
            this.loggerFactory.CreateLogger(null).ReturnsForAnyArgs(this.logger);
            this.federationSettings = Substitute.For<IFederationGatewaySettings>();
        }

        [Fact]
        public async Task SendBlockTip_Should_Be_Able_To_Send_IBlockTip()
        {
            this.PrepareWorkingHttpClient();

            var restSender = new RestBlockTipSender(this.loggerFactory, this.federationSettings, this.httpClientFactory);

            var blockTip = new BlockTipModel(TestingValues.GetUint256(), TestingValues.GetPositiveInt());

            await restSender.SendBlockTipAsync(blockTip);

            this.logger.Received(0).Log<object>(LogLevel.Error, 0, Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<Func<object, Exception, string>>());
        }

        private void PrepareWorkingHttpClient()
        {
            this.messageHandler = Substitute.ForPartsOf<HttpMessageHandler>();
            this.messageHandler.Protected("SendAsync", Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            this.httpClient = new HttpClient(this.messageHandler);

            this.httpClientFactory = Substitute.For<IHttpClientFactory>();
            this.httpClientFactory.CreateClient(Arg.Any<string>()).Returns(this.httpClient);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.messageHandler?.Dispose();
            this.httpClient?.Dispose();
        }
    }
}
