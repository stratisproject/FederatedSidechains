using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.NetworkHelpers;

namespace Stratis.FederatedPeg.Features.FederationGateway.SourceChain.Auditors
{
    internal class JsonTransferAuditor : ITransferAuditor
    {
        // The filename varies depending on the chain we are monitoring (mainchain=deposits or sidechain=withdrawals).
        private string filename;

        // The storage used to store the cross chain transactions.
        private readonly FileStorage<JsonTransferStore> fileStorage;

        // The in memory cross chain transaction store.
        private JsonTransferStore crossChainTransactionStore;

        public JsonTransferAuditor(Network network, DataFolder dataFolder)
        {
            this.fileStorage = new FileStorage<JsonTransferStore>(dataFolder.WalletPath);

            // Initialize chain specifics.
            var chain = network.ToChain();
            this.filename = chain == Chain.Mainchain ? "deposit_transaction_store.json" : "withdrawal_transaction_store.json";
        }

        public void Initialize()
        {
            // Load the store.
            this.crossChainTransactionStore = this.LoadCrossChainTransactionStore();
        }

        // Load the store (creates if no store yet).
        private JsonTransferStore LoadCrossChainTransactionStore()
        {
            if (this.fileStorage.Exists(this.filename))
                return this.fileStorage.LoadByFileName(this.filename);

            // Create a new empty store.
            var transactionStore = new JsonTransferStore();
            this.fileStorage.SaveToFile(transactionStore, this.filename);
            return transactionStore;
        }

        private void SaveCrossChainTransactionStore()
        {
            if (this.crossChainTransactionStore != null) //if initialize was not called
                this.fileStorage.SaveToFile(this.crossChainTransactionStore, this.filename);
        }

        public void AddTransferInfo(Transfer crossChainTransactionInfo)
        {
            this.crossChainTransactionStore.Add(crossChainTransactionInfo);
        }

        public void Load()
        {
            // Do nothing. Load is handled in Initialize
        }

        public void Commit()
        {
            this.SaveCrossChainTransactionStore();
        }

        public void AddTargetTransactionId(uint256 sessionId, uint256 counterChainTransactionId)
        {
            this.crossChainTransactionStore.AddCrossChainTransactionId(sessionId, counterChainTransactionId);
        }

        public void Dispose()
        {
            this.Commit();
        }
    }
}
