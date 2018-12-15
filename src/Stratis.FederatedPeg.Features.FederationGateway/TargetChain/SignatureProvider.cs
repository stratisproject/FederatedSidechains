using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Wallet;

namespace Stratis.FederatedPeg.Features.FederationGateway.TargetChain
{
    /// <summary>
    /// The purpose of this class is to sign externally provided withdrawal transactions if they
    /// are deemed valid. Transaction are signed in response to signature requests from the federation
    /// leader. The federation is required to be active as the wallet password is supplied during
    /// activation. Transaction's are validated to ensure that they are expected as per
    /// the deposits received on the source chain.
    /// </summary>
    public class SignatureProvider: ISignatureProvider
    {
        private readonly IFederationWalletManager federationWalletManager;
        private readonly ICrossChainTransferStore crossChainTransferStore;
        private readonly IFederationGatewaySettings federationGatewaySettings;
        private readonly Network network;
        private readonly ILogger logger;

        public SignatureProvider(
            IFederationWalletManager federationWalletManager,
            ICrossChainTransferStore crossChainTransferStore,
            IFederationGatewaySettings federationGatewaySettings,
            Network network,
            ILoggerFactory loggerFactory)
        {
            Guard.NotNull(federationWalletManager, nameof(federationWalletManager));
            Guard.NotNull(crossChainTransferStore, nameof(crossChainTransferStore));
            Guard.NotNull(federationGatewaySettings, nameof(federationGatewaySettings));
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.federationWalletManager = federationWalletManager;
            this.federationGatewaySettings = federationGatewaySettings;
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

        private Transaction SignTransaction(Transaction transaction, Key key)
        {
            return this.federationWalletManager.SignTransaction(transaction, IsAuthorized, key);
        }

        /// <inheritdoc />
        public string SignTransaction(string transactionHex)
        {
            Guard.NotNull(transactionHex, nameof(transactionHex));

            this.logger.LogTrace("():{0}", transactionHex);

            FederationWallet wallet = this.federationWalletManager.GetWallet();
            if (wallet == null || this.federationWalletManager.Secret == null)
            {
                this.logger.LogTrace("(-)[FEDERATION_INACTIVE]");
                return null;
            }

            Key key = wallet.MultiSigAddress.GetPrivateKey(wallet.EncryptedSeed, this.federationWalletManager.Secret.WalletPassword, this.network);
            if (key.PubKey.ToHex() != this.federationGatewaySettings.PublicKey)
            {
                this.logger.LogTrace("(-)[FEDERATION_KEY_INVALID]");
                return null;
            }

            Transaction transaction = this.network.CreateTransaction(transactionHex);

            transactionHex = SignTransaction(transaction, key)?.ToHex(this.network);

            this.logger.LogTrace("(-):{0}", transactionHex);

            return transactionHex;
        }
    }
}
