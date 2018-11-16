using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    public class SignedMultisigTransactionBroadcaster : ISignedMultisigTransactionBroadcaster, IDisposable
    {
        private readonly ILogger logger;
        private readonly IDisposable leaderReceiverSubscription;
        private readonly ICrossChainTransferStore store;
        private readonly string publicKey;

        public SignedMultisigTransactionBroadcaster(ILoggerFactory loggerFactory,
                                                    ICrossChainTransferStore store,
                                                    ILeaderReceiver leaderReceiver,
                                                    IFederationGatewaySettings settings)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.store = store;
            this.publicKey = settings.PublicKey;

            this.leaderReceiverSubscription = leaderReceiver.LeaderProvidersStream.Subscribe(async m => await BroadcastTransactionsAsync(m));
            this.logger.LogDebug("Subscribed to {0}", nameof(leaderReceiver), nameof(leaderReceiver.LeaderProvidersStream));
        }

        public async Task BroadcastTransactionsAsync(ILeaderProvider leaderProvider)
        {
            if (this.publicKey != leaderProvider.CurrentLeader.ToString()) return;

            var transactions = await this.store.GetSignedTransactionsAsync().ConfigureAwait(false);

            foreach(var transaction in transactions)
            {
                // TODO ignore transaction if it's in the mempool

                // Otherwise send the transaction
            }
        }

        public void Dispose()
        {
            this.store?.Dispose();
            this.leaderReceiverSubscription?.Dispose();
        }
    }
}
