using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.FederatedPeg.Features.FederationGateway.Controllers;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.SourceChain
{
    public class RestMaturedBlockSender : RestSenderBase, IMaturedBlockSender
    {
        private readonly ILogger logger;

        public RestMaturedBlockSender(ILoggerFactory loggerFactory, IFederationGatewaySettings settings, IHttpClientFactory httpClientFactory)
            : base(loggerFactory, settings, httpClientFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

        }

        /// <inheritdoc />
        public async Task SendMaturedBlockDepositsAsync(IMaturedBlockDeposits maturedBlockDeposits)
        {
            if (this.CanSend())
            {
                foreach (IDeposit deposit in maturedBlockDeposits.Deposits)
                {
                    this.logger.LogDebug("Mature deposit {0} ", deposit);
                }

                await this.SendAsync((MaturedBlockDepositsModel)maturedBlockDeposits, FederationGatewayRouteEndPoint.ReceiveMaturedBlocks).ConfigureAwait(false);
            }
        }
    }
}
