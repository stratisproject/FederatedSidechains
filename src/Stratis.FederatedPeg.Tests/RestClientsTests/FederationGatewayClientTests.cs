using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.FederatedPeg.Tests.RestClientsTests
{
    public class FederationGatewayClientTests : IDisposable
    {
        // TODO tests for SendMaturedBlockDepositsAsync

        //private IHttpClientFactory httpClientFactory;
        //
        //private HttpMessageHandler messageHandler;
        //
        //private HttpClient httpClient;
        //
        //private readonly ILoggerFactory loggerFactory;
        //
        //private readonly IFederationGatewaySettings federationSettings;
        //
        //private readonly ILogger logger;
        //
        //public RestMaturedBlockSenderTests()
        //{
        //    this.loggerFactory = Substitute.For<ILoggerFactory>();
        //    this.logger = Substitute.For<ILogger>();
        //    this.loggerFactory.CreateLogger(null).ReturnsForAnyArgs(this.logger);
        //    this.federationSettings = Substitute.For<IFederationGatewaySettings>();
        //}
        //
        //[Fact]
        //public async Task SendMaturedBlockDeposits_Should_Be_Able_To_Send_IMaturedBlockDepositsAsync()
        //{
        //    TestingHttpClient.PrepareWorkingHttpClient(ref this.messageHandler, ref this.httpClient, ref this.httpClientFactory);
        //
        //    IMaturedBlockDeposits maturedBlockDeposits = TestingValues.GetMaturedBlockDeposits();
        //
        //    var restSender = new RestMaturedBlockSender(this.loggerFactory, this.federationSettings, this.httpClientFactory);
        //
        //    await restSender.SendMaturedBlockDepositsAsync(maturedBlockDeposits).ConfigureAwait(false);
        //
        //    this.logger.Received(0).Log<object>(LogLevel.Error, 0, Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<Func<object, Exception, string>>());
        //}
        //
        //[Fact]
        //public async Task SendMaturedBlockDeposits_Should_Log_Error_When_Failing_To_Send_MaturedBlockDepositAsync()
        //{
        //    TestingHttpClient.PrepareFailingHttpClient(ref this.messageHandler, ref this.httpClient, ref this.httpClientFactory);
        //
        //    IMaturedBlockDeposits maturedBlockDeposits = TestingValues.GetMaturedBlockDeposits();
        //
        //    var restSender = new RestMaturedBlockSender(this.loggerFactory, this.federationSettings, this.httpClientFactory);
        //
        //    await restSender.SendMaturedBlockDepositsAsync(maturedBlockDeposits).ConfigureAwait(false);
        //
        //    this.logger.Received(1).Log<object>(LogLevel.Error, 0, Arg.Any<object>(), Arg.Is<Exception>(e => e == null), Arg.Any<Func<object, Exception, string>>());
        //}
        //

        /// <inheritdoc />
        public void Dispose()
        {
            //this.messageHandler?.Dispose();
            //this.httpClient?.Dispose();
        }



        // ========================= TESTS FOR PushCurrentBlockTipAsync

        //private IHttpClientFactory httpClientFactory;
        //
        //private HttpMessageHandler messageHandler;
        //
        //private HttpClient httpClient;
        //
        //private readonly ILoggerFactory loggerFactory;
        //
        //private readonly IFederationGatewaySettings federationSettings;
        //
        //private readonly ILogger logger;
        //
        //public RestBlockTipSenderTests()
        //{
        //    this.loggerFactory = Substitute.For<ILoggerFactory>();
        //    this.logger = Substitute.For<ILogger>();
        //    this.loggerFactory.CreateLogger(null).ReturnsForAnyArgs(this.logger);
        //    this.federationSettings = Substitute.For<IFederationGatewaySettings>();
        //}
        //
        //[Fact]
        //public async Task SendBlockTip_Should_Be_Able_To_Send_IBlockTipAsync()
        //{
        //    TestingHttpClient.PrepareWorkingHttpClient(ref this.messageHandler, ref this.httpClient, ref this.httpClientFactory);
        //
        //    var restSender = new RestBlockTipSender(this.loggerFactory, this.federationSettings, this.httpClientFactory);
        //
        //    var blockTip = new BlockTipModel(TestingValues.GetUint256(), TestingValues.GetPositiveInt(), TestingValues.GetPositiveInt());
        //
        //    await restSender.SendBlockTipAsync(blockTip).ConfigureAwait(false);
        //
        //    this.logger.Received(0).Log<object>(LogLevel.Error, 0, Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<Func<object, Exception, string>>());
        //}
        //
        //[Fact]
        //public async Task SendBlockTip_Should_Log_Error_When_Failing_To_Send_IBlockTipAsync()
        //{
        //    TestingHttpClient.PrepareFailingHttpClient(ref this.messageHandler, ref this.httpClient, ref this.httpClientFactory);
        //
        //    var restSender = new RestBlockTipSender(this.loggerFactory, this.federationSettings, this.httpClientFactory);
        //
        //    var blockTip = new BlockTipModel(TestingValues.GetUint256(), TestingValues.GetPositiveInt(), TestingValues.GetPositiveInt());
        //
        //    await restSender.SendBlockTipAsync(blockTip).ConfigureAwait(false);
        //
        //    this.logger.Received(1).Log<object>(LogLevel.Error, 0, Arg.Any<object>(), Arg.Is<Exception>(e => e == null), Arg.Any<Func<object, Exception, string>>());
        //}
        //
        ///// <inheritdoc />
        //public void Dispose()
        //{
        //    this.messageHandler?.Dispose();
        //    this.httpClient?.Dispose();
        //}
    }
}
