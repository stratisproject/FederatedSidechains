using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    /// <summary>
    /// Handles block syncing between gateways on 2 chains. This node will request
    /// blocks from another chain to look for cross chain deposit transactions.
    /// </summary>
    public class MaturedBlocksSyncManager : IDisposable
    {
        private readonly ICrossChainTransferStore store;

        private readonly ILogger logger;

        // TODO implement only pull mechanism


        public MaturedBlocksSyncManager(ILoggerFactory loggerFactory, ICrossChainTransferStore store)
        {
            this.store = store;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <summary>Starts requesting blocks from another chain.</summary>
        public void Initialize()
        {
            //TODO  start bg loop and ask for the first portion of blocks
        }

        /// <inheritdoc />
        public void Dispose()
        {

        }
    }
}
