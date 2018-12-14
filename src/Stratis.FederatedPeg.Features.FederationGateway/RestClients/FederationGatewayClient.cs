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
        public Task PushMaturedBlockAsync(MaturedBlockDepositsModel model)
        {
            return this.SendPostRequestAsync(model, FederationGatewayRouteEndPoint.PushMaturedBlocks);
        }

        /// <summary><see cref="FederationGatewayController.PushCurrentBlockTip"/></summary>
        public Task PushCurrentBlockTipAsync(BlockTipModel model)
        {
            return this.SendPostRequestAsync(model, FederationGatewayRouteEndPoint.PushCurrentBlockTip);
        }

        /// <summary><see cref="FederationGatewayController.GetMaturedBlockDepositsAsync"/></summary>
        public Task<List<IMaturedBlockDeposits>> GetMaturedBlockDepositsAsync(MaturedBlockRequestModel model)
        {
            return this.SendPostRequestAsync<MaturedBlockRequestModel, List<IMaturedBlockDeposits>>(model, FederationGatewayRouteEndPoint.GetMaturedBlockDeposits);
        }
    }
}
