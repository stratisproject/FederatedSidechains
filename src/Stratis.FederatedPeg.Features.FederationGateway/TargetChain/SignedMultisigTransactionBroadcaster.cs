using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    public class SignedMultisigTransactionBroadcaster : ISignedMultisigTransactionBroadcaster, IDisposable
    {
        private readonly ILogger logger;
        private readonly IDisposable leaderReceiverSubscription;
        private readonly ICrossChainTransferStore store;
        private readonly string thisLeadersPublicKey;
        private readonly MempoolManager mempoolManager;
        private readonly IBroadcasterManager broadcasterManager;

        public SignedMultisigTransactionBroadcaster(ILoggerFactory loggerFactory,
                                                    ICrossChainTransferStore store,
                                                    ILeaderReceiver leaderReceiver,
                                                    IFederationGatewaySettings settings,
                                                    MempoolManager mempoolManager,
                                                    IBroadcasterManager broadcasterManager)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.store = store;
            this.thisLeadersPublicKey = settings.PublicKey;
            this.mempoolManager = mempoolManager;
            this.broadcasterManager = broadcasterManager;

            this.leaderReceiverSubscription = leaderReceiver.LeaderProvidersStream.Subscribe(async m => await BroadcastTransactionsAsync(m));
            this.logger.LogDebug("Subscribed to {0}", nameof(leaderReceiver), nameof(leaderReceiver.LeaderProvidersStream));
        }

        public async Task BroadcastTransactionsAsync(ILeaderProvider leaderProvider)
        {
            if (this.thisLeadersPublicKey != leaderProvider.CurrentLeader.ToString()) return;

            var transactions = await this.store.GetSignedTransactionsAsync().ConfigureAwait(false);

            foreach (var transaction in transactions)
            {
                var transactionHash = transaction.GetHash();
                var txInfo = await this.mempoolManager.InfoAsync(transactionHash).ConfigureAwait(false);

                if (txInfo != null)
                {
                    this.logger.LogTrace("Transaction ID '{0}' already in the mempool.", transactionHash);
                    continue;
                }

                await this.broadcasterManager.BroadcastTransactionAsync(transaction).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            this.store?.Dispose();
            this.leaderReceiverSubscription?.Dispose();
        }
    }
}
