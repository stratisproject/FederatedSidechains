using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.FederatedPeg.Features.FederationGateway.Controllers;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.SourceChain
{
    public class RestMaturedBlockSender : IMaturedBlockSender
    {
        private readonly IHttpClientFactory httpClientFactory;

        private readonly ILogger logger;

        private readonly int targetApiPort;

        private readonly Uri publicationUri;

        public RestMaturedBlockSender(ILoggerFactory loggerFactory, IFederationGatewaySettings settings, IHttpClientFactory httpClientFactory)
        {
            this.httpClientFactory = httpClientFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.targetApiPort = settings.CounterChainApiPort;
            this.publicationUri = new Uri(
                $"http://localhost:{this.targetApiPort}/api/FederationGateway/{FederationGatewayController.ReceiveMaturedBlockRoute}");
        }

        /// <inheritdoc />
        public async Task SendMaturedBlockDepositsAsync(IMaturedBlockDeposits maturedBlockDeposits)
        {
            var maturedBlockDepositsModel = (MaturedBlockDepositsModel)maturedBlockDeposits;
            var request = new JsonContent(maturedBlockDepositsModel);
            using (var httpClient = this.httpClientFactory.CreateClient())
            {
                try
                {
                    var httpResponseMessage = await httpClient.PostAsync(this.publicationUri, request);
                    this.logger.LogDebug("Response: {0}", httpResponseMessage);
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Failed to send matured block {0}", maturedBlockDepositsModel);
                }
            }
        }
    }
}
