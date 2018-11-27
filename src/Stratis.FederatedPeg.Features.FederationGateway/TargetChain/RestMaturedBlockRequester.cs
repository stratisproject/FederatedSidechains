using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Stratis.FederatedPeg.Features.FederationGateway.Controllers;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;
using Stratis.FederatedPeg.Features.FederationGateway.SourceChain;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    public class RestMaturedBlockRequester : RestSenderBase, IMaturedBlocksRequester
    {
        public const int MaxBlocksToCatchup = 1000;

        private ICrossChainTransferStore crossChainTransferStore;
        private IMaturedBlockReceiver maturedBlockReceiver;
        private int maxDepositHeight;

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
        public void Start()
        {
            // Over-estimate. We correct this later.
            this.maxDepositHeight = int.MaxValue;
            this.GetMoreBlocksAsync().GetAwaiter().GetResult();
        }

        /// <inheritdoc />
        public async Task<bool> GetMoreBlocksAsync()
        {
            int maxBlocksToRequest = 1;

            if (!this.crossChainTransferStore.HasSuspended())
            {
                maxBlocksToRequest = Math.Min(MaxBlocksToCatchup, this.maxDepositHeight - this.crossChainTransferStore.NextMatureDepositHeight + 1);

                if (maxBlocksToRequest <= 0)
                    return false;
            }

            var model = new MaturedBlockRequestModel(this.crossChainTransferStore.NextMatureDepositHeight, maxBlocksToRequest);
            HttpResponseMessage response = await this.SendAsync(model, FederationGatewayRouteEndPoint.GetMaturedBlockDeposits).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                string successJson = response.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                if (successJson != null)
                {
                    MaturedBlockDepositsModel[] blockDeposits = JsonConvert.DeserializeObject<MaturedBlockDepositsModel[]>(successJson);

                    this.maturedBlockReceiver.ReceiveMaturedBlockDeposits(blockDeposits);

                    if (blockDeposits.Length < maxBlocksToRequest)
                    {
                        this.maxDepositHeight = model.BlockHeight + blockDeposits.Length - 1;
                        await this.crossChainTransferStore.SaveCurrentTipAsync().ConfigureAwait(false);
                    }

                    return true;
                }
            }

            return false;
        }
    }
}
