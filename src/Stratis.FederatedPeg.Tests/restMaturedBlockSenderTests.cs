using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;
using Stratis.FederatedPeg.Features.FederationGateway.SourceChain;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class RestMaturedBlockSenderTests : IDisposable
    {
        private IHttpClientFactory httpClientFactory;

        private HttpMessageHandler messageHandler;

        private HttpClient httpClient;

        private ILoggerFactory loggerFactory;

        private IFederationGatewaySettings federationSettings;

        private ILogger logger;

        public RestMaturedBlockSenderTests()
        {
            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.logger = Substitute.For<ILogger>();
            this.loggerFactory.CreateLogger(null).ReturnsForAnyArgs(this.logger);
            this.federationSettings = Substitute.For<IFederationGatewaySettings>();
        }

        [Fact]
        public async Task SendMaturedBlockDeposits_Should_Be_Able_To_Send_IMaturedBlockDeposits()
        {
            PrepareWorkingHttpClient();

            var maturedBlockDeposits = PrepareMaturedBlockDeposits();

            var restSender = new RestMaturedBlockSender(this.loggerFactory, this.federationSettings, this.httpClientFactory);

            await restSender.SendMaturedBlockDepositsAsync(maturedBlockDeposits);
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

        [Fact]
        public async Task SendMaturedBlockDeposits_Should_Log_Error_When_Failing_To_Send_MaturedBlockDeposit()
        {
            PrepareFailingHttpClient();

            var maturedBlockDeposits = PrepareMaturedBlockDeposits();

            var restSender = new RestMaturedBlockSender(this.loggerFactory, this.federationSettings, this.httpClientFactory);

            await restSender.SendMaturedBlockDepositsAsync(maturedBlockDeposits);
            this.logger.Received(1).Log<object>(LogLevel.Error, 0, Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<Func<object, Exception, string>>());
        }

        private void PrepareFailingHttpClient()
        {
            this.messageHandler = Substitute.ForPartsOf<HttpMessageHandler>();
            this.messageHandler.Protected("SendAsync", Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>())
                .ThrowsForAnyArgs(new Exception("failed"));
            this.httpClient = new HttpClient(this.messageHandler);

            this.httpClientFactory = Substitute.For<IHttpClientFactory>();
            this.httpClientFactory.CreateClient(Arg.Any<string>()).Returns(this.httpClient);
        }

        private static MaturedBlockDepositsModel PrepareMaturedBlockDeposits()
        {
            var blockHash = new uint256("82ae5390db507fc0f14325daa21cf55df08b9e14498b81f549dbd06eb72ab71e");
            var blockHeight = 9876;
            var depositId = new uint256("921ea22ac2db52669b4dde99fa0c432a1b04b47393b5dbe0027ad90a28b5e5cf");
            var depositAmount = Money.Coins(13);
            var targetAddress = "somewhereontheblockchain";

            var maturedBlockDeposits = new MaturedBlockDepositsModel(
                new MaturedBlockModel() { BlockHash = blockHash, BlockHeight = blockHeight },
                new List<IDeposit>() { new Deposit(depositId, depositAmount, targetAddress, blockHeight, blockHash) });
            return maturedBlockDeposits;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.messageHandler?.Dispose();
            this.httpClient?.Dispose();
        }
    }
}
