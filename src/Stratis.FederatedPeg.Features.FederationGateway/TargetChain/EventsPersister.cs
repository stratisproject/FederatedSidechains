using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    public class EventsPersister : IEventPersister, IDisposable
    {
        private readonly IMaturedBlockReceiver maturedBlockReceiver;

        private readonly ILogger logger;

        private readonly IDisposable maturedBlockDepositSubscription;
        
        private readonly ICrossChainTransferStore store;
        
        public EventsPersister(ILoggerFactory loggerFactory,
                               ICrossChainTransferStore store,
                               IMaturedBlockReceiver maturedBlockReceiver)
        {
            this.maturedBlockReceiver = maturedBlockReceiver;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.store = store;

            this.maturedBlockDepositSubscription = maturedBlockReceiver.MaturedBlockDepositStream.Subscribe(async m => await PersistNewMaturedBlockDeposits(m));
        }

        /// <inheritdoc />
        public async Task PersistNewMaturedBlockDeposits(IMaturedBlockDeposits maturedBlockDeposits)
        {
            await store.RecordLatestMatureDepositsAsync(maturedBlockDeposits.Deposits.ToArray());
        }

        /// <inheritdoc />
        public Task PersistNewSourceChainTip(IBlockTip newTip)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.store?.Dispose();
            this.maturedBlockDepositSubscription?.Dispose();
        }
    }
}
