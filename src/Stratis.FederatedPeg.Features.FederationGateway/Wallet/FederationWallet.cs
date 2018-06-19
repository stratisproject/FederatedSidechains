using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.FederatedPeg.Features.FederationGateway.Wallet
{
    /// <summary>
    /// A wallet containing addresses not derived from an HD seed.
    /// Also sometimes referred to as a JBOK (Just a Bunch Of Keys)
    /// wallet.
    /// </summary>
    public class FederationWallet
    {
        private ILogger logger;

        /// <summary>
        /// The seed for this wallet's multisig addresses, password encrypted.
        /// </summary>
        [JsonProperty(PropertyName = "encryptedSeed")]
        public string EncryptedSeed { get; set; }

        /// <summary>
        /// Gets or sets the merkle path.
        /// </summary>
        [JsonProperty(PropertyName = "blockLocator", ItemConverterType = typeof(UInt256JsonConverter))]
        public ICollection<uint256> BlockLocator { get; set; }

        /// <summary>
        /// The network this wallet contains addresses and transactions for.
        /// </summary>
        [JsonProperty(PropertyName = "network")]
        [JsonConverter(typeof(NetworkConverter))]
        public Network Network { get; set; }

        /// <summary>
        /// The time this wallet was created.
        /// </summary>
        [JsonProperty(PropertyName = "creationTime")]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset CreationTime { get; set; }

        /// <summary>
        /// The height of the last block that was synced.
        /// </summary>
        [JsonProperty(PropertyName = "lastBlockSyncedHeight", NullValueHandling = NullValueHandling.Ignore)]
        public int? LastBlockSyncedHeight { get; set; }

        /// <summary>
        /// The hash of the last block that was synced.
        /// </summary>
        [JsonProperty(PropertyName = "lastBlockSyncedHash", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 LastBlockSyncedHash { get; set; }

        /// <summary>
        /// The type of coin, Bitcoin or Stratis.
        /// </summary>
        [JsonProperty(PropertyName = "coinType")]
        public CoinType CoinType { get; set; }

        /// <summary>
        /// The multisig address, where this node is one of several signatories to transactions.
        /// </summary>
        [JsonProperty(PropertyName = "multiSigAddress")]
        public MultiSigAddress MultiSigAddress { get; set; }

        public FederationWallet()
        {
            //keep that for serialisation
        }

        public FederationWallet(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <summary>
        /// Gets a collection of transactions with spendable outputs.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<TransactionData> GetSpendableTransactions()
        {
            return this.MultiSigAddress.Transactions.Where(t => t.IsSpendable());
        }

        /// <summary>
        /// Lists all spendable transactions in the current wallet.
        /// </summary>
        /// <param name="currentChainHeight">The current height of the chain. Used for calculating the number of confirmations a transaction has.</param>
        /// <param name="confirmations">The minimum number of confirmations required for transactions to be considered.</param>
        /// <returns>A collection of spendable outputs that belong to the given account.</returns>
        public IEnumerable<UnspentOutputReference> GetSpendableTransactions(int currentChainHeight, int confirmations = 0)
        {
            // A block that is at the tip has 1 confirmation.
            // When calculating the confirmations the tip must be advanced by one.

            int countFrom = currentChainHeight + 1;
            foreach (TransactionData transactionData in this.GetSpendableTransactions())
            {
                int? confirmationCount = 0;
                if (transactionData.BlockHeight != null)
                    confirmationCount = countFrom >= transactionData.BlockHeight ? countFrom - transactionData.BlockHeight : 0;

                if (confirmationCount >= confirmations)
                {
                    yield return new UnspentOutputReference
                    {
                        Transaction = transactionData,
                    };
                }
            }
        }

        /// <summary>
        /// Get the accounts total spendable value for both confirmed and unconfirmed UTXO.
        /// </summary>
        public (Money ConfirmedAmount, Money UnConfirmedAmount) GetSpendableAmount()
        {
            var confirmed = this.MultiSigAddress.Transactions.Sum(t => t.SpendableAmount(true));
            var total = this.MultiSigAddress.Transactions.Sum(t => t.SpendableAmount(false));

            return (confirmed, total - confirmed);
        }

        public Transaction SignPartialTransaction(Transaction partial, string password)
        {
            logger.LogTrace("Signing partial transaction {0}", partial);
            // Need to get the same ScriptCoins used by the other signatories.
            // It is assumed that the funds are present in the MultiSigAddress
            // transactions.

            // Find the transaction(s) in the MultiSigAddress that have the
            // referenced inputs among their outputs.


            List<Transaction> fundingTransactions = new List<Transaction>();

            foreach (TransactionData tx in this.MultiSigAddress.Transactions)
            {
                Transaction trx = tx.GetFullTransaction(this.Network);

                foreach (var output in trx.Outputs.AsIndexedOutputs())
                {
                    foreach (var input in partial.Inputs)
                    {
                        if (input.PrevOut.Hash == tx.Id && input.PrevOut.N == output.N)
                            fundingTransactions.Add(trx);
                    }
                }
            }
            logger.LogTrace("found {0} funding transactions for partial transaction {1}", fundingTransactions.Count, partial.GetHash());

            // Then convert the outputs to Coins & make ScriptCoins out of them.
            List<ScriptCoin> scriptCoins = new List<ScriptCoin>();

            foreach (var tx in fundingTransactions)
            {
                scriptCoins.AddRange(tx.Outputs.AsCoins()
                    .Where(coin => coin.ScriptPubKey == this.MultiSigAddress.ScriptPubKey)
                    .Select(coin => coin.ToScriptCoin(this.MultiSigAddress.RedeemScript)));
            }
            logger.LogTrace("found {0} scriptCoins from funding transactions for partial transaction {1}", scriptCoins.Count, partial.GetHash());


            // Need to construct a transaction using a transaction builder with
            // the appropriate state
            TransactionBuilder builder = new TransactionBuilder(this.Network);

            var privateKey = this.MultiSigAddress.GetPrivateKey(this.EncryptedSeed, password, this.Network);
            //todo: remove that before release
            logger.LogTrace("retrieved private key {0} from encrypted seed {1}", privateKey, EncryptedSeed);
            Transaction signedPartialTransaction = builder
                .AddCoins(scriptCoins)
                .AddKeys(privateKey)
                .SignTransaction(partial);

            logger.LogTrace("signed partial transaction {0}", partial);

            return signedPartialTransaction;
        }

        public Transaction CombinePartialTransactions(Transaction[] partials)
        {
            logger.LogTrace("Combining {0} partial transactions", partials.Length);

            Transaction firstPartial = partials[0];
            logger.LogTrace("Retrieving inputs and outputs using first partial transaction {0} to find inputs", partials.Length);

            // Need to get the same ScriptCoins used by the other signatories.
            // It is assumed that the funds are present in the MultiSigAddress
            // transactions.

            // Find the transaction(s) in the MultiSigAddress that have the
            // referenced inputs among their outputs.

            List<Transaction> fundingTransactions = new List<Transaction>();

            foreach (TransactionData tx in this.MultiSigAddress.Transactions)
            {
                Transaction trx = tx.GetFullTransaction(this.Network);
                foreach (var output in trx.Outputs.AsIndexedOutputs())
                {
                    foreach (var input in firstPartial.Inputs)
                    {
                        if (input.PrevOut.Hash == tx.Id && input.PrevOut.N == output.N)
                            fundingTransactions.Add(trx);
                    }
                }
            }
            logger.LogTrace("found {0} funding transactions for partial transaction {1}", fundingTransactions.Count, firstPartial.GetHash());


            // Then convert the outputs to Coins & make ScriptCoins out of them.

            List<ScriptCoin> scriptCoins = new List<ScriptCoin>();

            foreach (var tx in fundingTransactions)
            {
                scriptCoins.AddRange(tx.Outputs.AsCoins()
                    .Where(coin => coin.ScriptPubKey == this.MultiSigAddress.ScriptPubKey)
                    .Select(coin => coin.ToScriptCoin(this.MultiSigAddress.RedeemScript)));
            }
            logger.LogTrace("found {0} scriptCoins from funding transactions for partial transaction {1}", scriptCoins.Count, firstPartial.GetHash());
            // Need to construct a transaction using a transaction builder with
            // the appropriate state

            TransactionBuilder builder = new TransactionBuilder(this.Network);

            Transaction combined = builder
                    .AddCoins(scriptCoins)
                    .CombineSignatures(partials);
            logger.LogTrace("built combined transaction {0} from {1} partials", combined, partials.Length);

            return combined;
        }
    }
}