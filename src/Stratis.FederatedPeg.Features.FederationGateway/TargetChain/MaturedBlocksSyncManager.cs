using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    // TODO remove EventsPersister MaturedBlockReceiver RestMaturedBlockRequester

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

        private readonly ILogger logger;

        // TODO implement only pull mechanism

        private readonly CancellationTokenSource cancellation;

        private Task blockRequestingTask;


        public MaturedBlocksSyncManager(ILoggerFactory loggerFactory, ICrossChainTransferStore store)
        {
            this.store = store;

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
                // TODO ask for the portion of blocks
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
