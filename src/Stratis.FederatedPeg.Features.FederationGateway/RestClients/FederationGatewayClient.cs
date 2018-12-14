using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.FederatedPeg.Features.FederationGateway.Controllers;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.RestClients
{
    /// <summary>Rest client for <see cref="FederationGatewayController"/>.</summary>
    public class FederationGatewayClient : RestApiClientBase
    {
        public FederationGatewayClient(ILoggerFactory loggerFactory, IFederationGatewaySettings settings, IHttpClientFactory httpClientFactory)
            : base(loggerFactory, settings, httpClientFactory)
        {
        }

        /// <summary><see cref="FederationGatewayController.PushMaturedBlock"/></summary>
        public async Task PushMaturedBlockAsync(MaturedBlockDepositsModel model)
        {
            await this.SendPostRequestAsync(model, FederationGatewayRouteEndPoint.PushMaturedBlocks).ConfigureAwait(false);
        }

        /// <summary><see cref="FederationGatewayController.PushCurrentBlockTip"/></summary>
        public async Task PushCurrentBlockTipAsync(BlockTipModel model)
        {
            await this.SendPostRequestAsync(model, FederationGatewayRouteEndPoint.PushCurrentBlockTip).ConfigureAwait(false);
        }

        /// <summary><see cref="FederationGatewayController.GetMaturedBlockDepositsAsync"/></summary>
        public async Task<List<IMaturedBlockDeposits>> GetMaturedBlockDepositsAsync(MaturedBlockRequestModel model)
        {
            return await this.SendPostRequestAsync<MaturedBlockRequestModel, List<IMaturedBlockDeposits>>(model, FederationGatewayRouteEndPoint.GetMaturedBlockDeposits).ConfigureAwait(false);
        }
    }
}
