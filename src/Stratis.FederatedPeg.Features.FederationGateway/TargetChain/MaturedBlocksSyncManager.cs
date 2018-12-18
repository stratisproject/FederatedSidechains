using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;
using Stratis.FederatedPeg.Features.FederationGateway.RestClients;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    // TODO remove EventsPersister MaturedBlockReceiver RestMaturedBlockRequester

    // TODO implement only pull mechanism

    /// <summary>
    /// Handles block syncing between gateways on 2 chains. This node will request
    /// blocks from another chain to look for cross chain deposit transactions.
    /// </summary>
    public interface IMaturedBlocksSyncManager : IDisposable
    {
        /// <summary>Starts requesting blocks from another chain.</summary>
        void Initialize();
    }

    /// <inheritdoc cref="IMaturedBlocksSyncManager"/>
    public class MaturedBlocksSyncManager : IMaturedBlocksSyncManager
    {
        private readonly ICrossChainTransferStore store;

        private readonly IFederationGatewayClient federationGatewayClient;

        private readonly ILogger logger;

        private readonly CancellationTokenSource cancellation;

        private Task blockRequestingTask;

        /// <summary>The maximum amount of blocks to request at a time from alt chain.</summary>
        public const int MaxBlocksToRequest = 1000;

        public MaturedBlocksSyncManager(ICrossChainTransferStore store, IFederationGatewayClient federationGatewayClient, ILoggerFactory loggerFactory)
        {
            this.store = store;
            this.federationGatewayClient = federationGatewayClient;

            this.cancellation = new CancellationTokenSource();
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public void Initialize() // TODO initialize only after store is initialized
        {
            this.blockRequestingTask = RequestMaturedBlocksContinouslyAsync();
        }

        /// <summary>Continuously requests matured blocks from another chain.</summary>
        private async Task RequestMaturedBlocksContinouslyAsync()
        {
            while (!this.cancellation.IsCancellationRequested)
            {
                int blocksToRequest = 1;

                // TODO why are we asking for max of 1 block and if it's not suspended then 1000? investigate this logic
                if (!this.store.HasSuspended())
                    blocksToRequest = MaxBlocksToRequest;

                var model = new MaturedBlockRequestModel(this.store.NextMatureDepositHeight, blocksToRequest);

                this.logger.LogDebug("Request model created: '{0}'.", model);

                // Ask for blocks.
                IList<MaturedBlockDepositsModel> blockDeposits = await this.federationGatewayClient.GetMaturedBlockDepositsAsync(model).ConfigureAwait(false);

                if ((blockDeposits != null) && (blockDeposits.Count > 0))
                {
                    //bool result = await this.store.RecordLatestMatureDepositsAsync(blockDeposits).ConfigureAwait(false);
                    //
                    //// TODO what to do with result?
                    //
                    //
                    //this.maturedBlockReceiver.PushMaturedBlockDeposits(blockDeposits.ToArray()); // TODO PUSH TO STORE ITSELF
                    //
                    //if (blockDeposits.Count < blocksToRequest)
                    //{
                    //    await this.crossChainTransferStore.SaveCurrentTipAsync().ConfigureAwait(false);
                    //}
                }





                // TODO ask for the portion of blocks



                // this.store.RecordLatestMatureDepositsAsync(maturedBlockDeposits)
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.cancellation.Cancel();
            this.blockRequestingTask?.GetAwaiter().GetResult();
        }
    }
}
