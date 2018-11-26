using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.Controllers;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;
using Stratis.FederatedPeg.Features.FederationGateway.SourceChain;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    public class RestMaturedBlockRequester : RestSenderBase, IMaturedBlocksRequester, IDisposable
    {
        public const int MaxBlocksToCatchup = 1000;
        public static TimeSpan CatchUpInterval = TimeSpans.TenSeconds;

        private IAsyncLoop asyncLoop;
        private IAsyncLoopFactory asyncLoopFactory;
        private ICrossChainTransferStore crossChainTransferStore;
        private INodeLifetime nodeLifetime;
        private int maxDepositHeight;

        public RestMaturedBlockRequester(
            ILoggerFactory loggerFactory,
            IFederationGatewaySettings settings,
            IHttpClientFactory httpClientFactory,
            IAsyncLoopFactory asyncLoopFactory,
            ICrossChainTransferStore crossChainTransferStore,
            INodeLifetime nodeLifetime)
            : base(loggerFactory, settings, httpClientFactory)
        {
            this.asyncLoopFactory = asyncLoopFactory;
            this.crossChainTransferStore = crossChainTransferStore;
            this.nodeLifetime = nodeLifetime;
        }

        /// <inheritdoc />
        public void SetTip(int tipHeight)
        {
            this.maxDepositHeight = tipHeight;
        }

        /// <inheritdoc />
        public void SetLastReceived(int lastReceived)
        {
            if (lastReceived > this.maxDepositHeight)
            {
                this.maxDepositHeight = lastReceived;
            }
        }

        /// <inheritdoc />
        public void Start()
        {
            // Over-estimate. We correct this later.
            this.maxDepositHeight = int.MaxValue;

            CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(this.nodeLifetime.ApplicationStopping);
            this.asyncLoop = this.asyncLoopFactory.Run(nameof(RestMaturedBlockRequester), token =>
            {
                try
                {
                    while (this.crossChainTransferStore.NextMatureDepositHeight <= this.maxDepositHeight)
                    {
                        // We are behind the chain A tip.
                        int maxBlocksToRequest = 1;
                        maxBlocksToRequest = Math.Min(MaxBlocksToCatchup, this.maxDepositHeight - this.crossChainTransferStore.NextMatureDepositHeight + 1);

                        if (this.crossChainTransferStore.HasSuspended() || maxBlocksToRequest <= 0)
                        {
                            Thread.Sleep(TimeSpans.TenSeconds);
                            continue;
                        }

                        var model = new MaturedBlockRequestModel(this.crossChainTransferStore.NextMatureDepositHeight, maxBlocksToRequest);
                        HttpResponseMessage response = this.SendAsync(model, FederationGatewayRouteEndPoint.GetMaturedBlockDeposits).GetAwaiter().GetResult();
                        if (response?.IsSuccessStatusCode ?? false)
                        {
                            string successJson = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            MaturedBlockDepositsModel[] blockDeposits = JsonConvert.DeserializeObject<MaturedBlockDepositsModel[]>(successJson);

                            // We over-estimate the maxDepositHeight at start up.
                            // Set it to the correct value based on the blocks that we are able to retrieve
                            if (blockDeposits.Length < maxBlocksToRequest)
                                this.maxDepositHeight = model.BlockHeight + blockDeposits.Length - 1;

                            foreach (MaturedBlockDepositsModel blockDeposit in blockDeposits)
                            {
                                if (blockDeposit.Block.BlockHeight == this.crossChainTransferStore.NextMatureDepositHeight)
                                {
                                    this.crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposit.Deposits.ToArray()).GetAwaiter().GetResult();
                                }
                            }
                        }
                        else
                        {
                            Thread.Sleep(TimeSpans.TenSeconds);
                        }
                    }
                }
                catch (Exception e)
                {
                    linkedTokenSource.Cancel();
                }

                return Task.CompletedTask;
            },
            linkedTokenSource.Token,
            repeatEvery: CatchUpInterval);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (this.asyncLoop != null)
            {
                this.asyncLoop.Dispose();
                this.asyncLoop = null;
            }
        }
    }
}
