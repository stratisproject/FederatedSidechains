﻿using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.FederatedPeg.Features.FederationGateway.Controllers;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.RestClients
{
    /// <summary>Rest client for <see cref="FederationGatewayController"/>.</summary>
    public interface IFederationGatewayClient
    {
        /// <summary><see cref="FederationGatewayController.PushCurrentBlockTip"/></summary>
        Task<HttpResponseMessage> PushCurrentBlockTipAsync(BlockTipModel model, CancellationToken cancellation = default(CancellationToken));

        /// <summary><see cref="FederationGatewayController.GetMaturedBlockDepositsAsync"/></summary>
        Task<List<MaturedBlockDepositsModel>> GetMaturedBlockDepositsAsync(MaturedBlockRequestModel model, CancellationToken cancellation = default(CancellationToken));

        /// <summary><see cref="FederationGatewayController.GetBlockHeightClosestToTimestamp"/></summary>
        Task<ClosestHeightModel> GetBlockHeightClosestToTimestampAsync(uint timestamp, CancellationToken cancellation = default(CancellationToken));
    }

    /// <inheritdoc cref="IFederationGatewayClient"/>
    public class FederationGatewayClient : RestApiClientBase, IFederationGatewayClient
    {
        public FederationGatewayClient(ILoggerFactory loggerFactory, IFederationGatewaySettings settings, IHttpClientFactory httpClientFactory)
            : base(loggerFactory, settings, httpClientFactory)
        {
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> PushCurrentBlockTipAsync(BlockTipModel model, CancellationToken cancellation = default(CancellationToken))
        {
            return this.SendPostRequestAsync(model, FederationGatewayRouteEndPoint.PushCurrentBlockTip, cancellation);
        }

        /// <inheritdoc />
        public Task<List<MaturedBlockDepositsModel>> GetMaturedBlockDepositsAsync(MaturedBlockRequestModel model, CancellationToken cancellation = default(CancellationToken))
        {
            return this.SendPostRequestAsync<MaturedBlockRequestModel, List<MaturedBlockDepositsModel>>(model, FederationGatewayRouteEndPoint.GetMaturedBlockDeposits, cancellation);
        }

        /// <inheritdoc />
        public Task<ClosestHeightModel> GetBlockHeightClosestToTimestampAsync(uint timestamp, CancellationToken cancellation = default(CancellationToken))
        {
            string parameters = $"{nameof(timestamp)}={timestamp}";

            return this.SendGetRequestAsync<ClosestHeightModel>(FederationGatewayRouteEndPoint.GetBlockHeightClosestToTimestamp, cancellation, parameters);
        }
    }
}
