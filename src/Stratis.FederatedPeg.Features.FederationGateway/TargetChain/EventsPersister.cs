using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    public class EventsPersister : IEventPersister, IDisposable
    {
        private readonly IMaturedBlockReceiver maturedBlockReceiver;

        private readonly ILogger logger;

        private readonly IDisposable maturedBlockDepositSubscription;

        /// <inheritdoc />
        public ICrossChainTransferStore Store { get; }
        
        public EventsPersister(ILoggerFactory loggerFactory,
                               ICrossChainTransferStore store,
                               IMaturedBlockReceiver maturedBlockReceiver)
        {
            this.maturedBlockReceiver = maturedBlockReceiver;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.Store = store;

            this.maturedBlockDepositSubscription = maturedBlockReceiver.MaturedBlockDepositStream.Subscribe(async m => await PersistNewMaturedBlockDeposits(m));
        }

        /// <inheritdoc />
        public async Task PersistNewMaturedBlockDeposits(IMaturedBlockDeposits maturedBlockDeposits)
        {
            var depositIds = maturedBlockDeposits.Deposits.Select(d => d.Id).ToArray();
            var knownDepositIds = (await Store.GetAsync(depositIds)).Select(t => t.DepositTransactionId);

            var newDeposits = maturedBlockDeposits.Deposits.Where(d => !knownDepositIds.Contains(d.Id));

            if (!newDeposits.Any()) return;
            var depositBlockHeight = maturedBlockDeposits.Block.BlockHeight;

            var crossChainTransfers = newDeposits.Select(d =>
                new CrossChainTransfer(CrossChainTransferStatus.Partial, d.Id, depositBlockHeight, new Script(d.TargetAddress), d.Amount, null, null, -1));

            //this will not do anything : RecordLatestMatureDepositsAsync expects all the crosschain transfer statuses to be different from partial...
            //I don't understand why, or maybe itis expected that we build a template transaction before doing any persistence ?
            //I need to understand why there is no persistence of the transaction before it is actually built :)

            await Store.RecordLatestMatureDepositsAsync(crossChainTransfers);
        }

        /// <inheritdoc />
        public Task PersistNewSourceChainTip(IBlockTip newTip)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.Store?.Dispose();
            this.maturedBlockDepositSubscription?.Dispose();
        }
    }
}
