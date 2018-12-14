﻿using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Stratis.FederatedPeg.Features.FederationGateway.Controllers;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;
using Stratis.FederatedPeg.Features.FederationGateway.SourceChain;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    public interface IMaturedBlocksRequester
    {
        /// <summary>
        /// Gets more blocks from the counter node.
        /// </summary>
        /// <returns><c>True</c> if more blocks were found and <c>false</c> otherwise.</returns>
        Task<bool> GetMoreBlocksAsync();
    }

    public class RestMaturedBlockRequester : RestSenderBase, IMaturedBlocksRequester
    {
        public const int MaxBlocksToCatchup = 1000;

        private readonly ICrossChainTransferStore crossChainTransferStore;
        private readonly IMaturedBlockReceiver maturedBlockReceiver;

        public RestMaturedBlockRequester(
            ILoggerFactory loggerFactory,
            IFederationGatewaySettings settings,
            IHttpClientFactory httpClientFactory,
            ICrossChainTransferStore crossChainTransferStore,
            IMaturedBlockReceiver maturedBlockReceiver)
            : base(loggerFactory, settings, httpClientFactory)
        {
            this.crossChainTransferStore = crossChainTransferStore;
            this.maturedBlockReceiver = maturedBlockReceiver;
        }

        /// <inheritdoc />
        public async Task<bool> GetMoreBlocksAsync()
        {
            if (!this.CanSend())
                return false;

            int maxBlocksToRequest = 1;

            if (!this.crossChainTransferStore.HasSuspended())
            {
                maxBlocksToRequest = MaxBlocksToCatchup;
            }

            var model = new MaturedBlockRequestModel(this.crossChainTransferStore.NextMatureDepositHeight, maxBlocksToRequest);
            HttpResponseMessage response = await this.SendAsync(model, FederationGatewayRouteEndPoint.GetMaturedBlockDeposits).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                string successJson = response.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                if (successJson != null)
                {
                    MaturedBlockDepositsModel[] blockDeposits = JsonConvert.DeserializeObject<MaturedBlockDepositsModel[]>(successJson);

                    if (blockDeposits.Length > 0)
                    {
                        this.maturedBlockReceiver.PushMaturedBlockDeposits(blockDeposits);

                        if (blockDeposits.Length < maxBlocksToRequest)
                        {
                            await this.crossChainTransferStore.SaveCurrentTipAsync().ConfigureAwait(false);
                        }
                    }

                    return true;
                }
            }

            return false;
        }
    }
}
