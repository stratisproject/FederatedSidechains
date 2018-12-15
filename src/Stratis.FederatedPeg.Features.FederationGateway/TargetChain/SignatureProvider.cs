using System.Linq;
using NBitcoin;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    public class SignatureProvider: ISignatureProvider
    {
        private readonly IFederationWalletManager federationWalletManager;
        private readonly ICrossChainTransferStore crossChainTransferStore;
        private readonly Network network;

        public SignatureProvider(
            IFederationWalletManager federationWalletManager,
            ICrossChainTransferStore crossChainTransferStore,
            Network network)
        {
            this.federationWalletManager = federationWalletManager;
            this.crossChainTransferStore = crossChainTransferStore;
            this.network = network;
        }

        /// <summary>
        /// Determines if a withdrawal transaction can be authorized.
        /// </summary>
        /// <param name="transaction">The transaction to authorize.</param>
        /// <param name="withdrawal">The withdrawal transaction already extracted from the transaction.</param>
        /// <returns><c>True</c> if the withdrawal is valid and <c>false</c> otherwise.</returns>
        private bool IsAuthorized(Transaction transaction, IWithdrawal withdrawal)
        {
            // It must be a transfer that we know about.
            ICrossChainTransfer crossChainTransfer = this.crossChainTransferStore.GetAsync(new[] { withdrawal.DepositId }).GetAwaiter().GetResult().FirstOrDefault();
            if (crossChainTransfer == null)
                return false;

            // If its already been seen in a block then we probably should not authorize it.
            if (crossChainTransfer.Status == CrossChainTransferStatus.SeenInBlock)
                return false;

            // The templates must match what we expect to see.
            if (!CrossChainTransfer.TemplatesMatch(crossChainTransfer.PartialTransaction, transaction))
                return false;

            return true;
        }

        private Transaction SignTransaction(Transaction transaction)
        {
            return this.federationWalletManager.SignTransaction(transaction, IsAuthorized);
        }

        /// <inheritdoc />
        public string SignTransaction(string transactionHex)
        {
            Transaction transaction = this.network.CreateTransaction(transactionHex);
            return SignTransaction(transaction)?.ToHex(this.network);
        }
    }
}
