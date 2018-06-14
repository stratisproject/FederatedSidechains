using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Bitcoin.Features.GeneralPurposeWallet
{
	/// <summary>
	/// A wallet containing addresses not derived from an HD seed.
	/// Also sometimes referred to as a JBOK (Just a Bunch Of Keys)
	/// wallet.
	/// </summary>
	public class GeneralPurposeWallet
	{
		/// <summary>
		/// Initializes a new instance of the general purpose wallet.
		/// </summary>
		public GeneralPurposeWallet()
		{
		}

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
            // Need to get the same ScriptCoins used by the other signatories.
            // It is assumed that the funds are present in the MultiSigAddress
            // transactions.

            // Find the transaction(s) in the MultiSigAddress that have the
            // referenced inputs among their outputs.

            List<Transaction> fundingTransactions = new List<Transaction>();

            foreach (TransactionData tx in this.MultiSigAddress.Transactions)
            {
                foreach (var output in tx.Transaction.Outputs.AsIndexedOutputs())
                {
                    foreach (var input in partial.Inputs)
                    {
                        if (input.PrevOut.Hash == tx.Id && input.PrevOut.N == output.N)
                            fundingTransactions.Add(tx.Transaction);
                    }
                }
            }

            // Then convert the outputs to Coins & make ScriptCoins out of them.

            List<ScriptCoin> scriptCoins = new List<ScriptCoin>();

            foreach (var tx in fundingTransactions)
            {
                foreach (var coin in tx.Outputs.AsCoins())
                {
                    // Only care about outputs for our particular multisig
                    if (coin.ScriptPubKey == this.MultiSigAddress.ScriptPubKey)
                    {
                        scriptCoins.Add(coin.ToScriptCoin(this.MultiSigAddress.RedeemScript));
                    }
                }
            }

            // Need to construct a transaction using a transaction builder with
            // the appropriate state

            TransactionBuilder builder = new TransactionBuilder(this.Network);

            Transaction signed =
                builder
                    .AddCoins(scriptCoins)
                    .AddKeys(this.MultiSigAddress.GetPrivateKey(this.EncryptedSeed, password, this.Network))
                    .SignTransaction(partial);

            return signed;
        }

        public Transaction CombinePartialTransactions(Transaction[] partials)
        {
            Transaction firstPartial = partials[0];

            // Need to get the same ScriptCoins used by the other signatories.
            // It is assumed that the funds are present in the MultiSigAddress
            // transactions.

            // Find the transaction(s) in the MultiSigAddress that have the
            // referenced inputs among their outputs.

            List<Transaction> fundingTransactions = new List<Transaction>();

            foreach (TransactionData tx in this.MultiSigAddress.Transactions)
            {
                foreach (var output in tx.Transaction.Outputs.AsIndexedOutputs())
                {
                    foreach (var input in firstPartial.Inputs)
                    {
                        if (input.PrevOut.Hash == tx.Id && input.PrevOut.N == output.N)
                            fundingTransactions.Add(tx.Transaction);
                    }
                }
            }

            // Then convert the outputs to Coins & make ScriptCoins out of them.

            List<ScriptCoin> scriptCoins = new List<ScriptCoin>();

            foreach (var tx in fundingTransactions)
            {
                foreach (var coin in tx.Outputs.AsCoins())
                {
                    // Only care about outputs for our particular multisig
                    if (coin.ScriptPubKey == this.MultiSigAddress.ScriptPubKey)
                    {
                        scriptCoins.Add(coin.ToScriptCoin(this.MultiSigAddress.RedeemScript));
                    }
                }
            }

            // Need to construct a transaction using a transaction builder with
            // the appropriate state

            TransactionBuilder builder = new TransactionBuilder(this.Network);

            Transaction combined =
                builder
                    .AddCoins(scriptCoins)
                    .CombineSignatures(partials);

            return combined;
        }
	}

	/// <summary>
	/// An object containing transaction data.
	/// </summary>
	public class TransactionData
	{
		/// <summary>
		/// Transaction id.
		/// </summary>
		[JsonProperty(PropertyName = "id")]
		[JsonConverter(typeof(UInt256JsonConverter))]
		public uint256 Id { get; set; }

		/// <summary>
		/// The transaction amount.
		/// </summary>
		[JsonProperty(PropertyName = "amount")]
		[JsonConverter(typeof(MoneyJsonConverter))]
		public Money Amount { get; set; }

		/// <summary>
		/// The index of this scriptPubKey in the transaction it is contained.
		/// </summary>
		/// <remarks>
		/// This is effectively the index of the output, the position of the output in the parent transaction.
		/// </remarks>
		[JsonProperty(PropertyName = "index", NullValueHandling = NullValueHandling.Ignore)]
		public int Index { get; set; }

		/// <summary>
		/// The height of the block including this transaction.
		/// </summary>
		[JsonProperty(PropertyName = "blockHeight", NullValueHandling = NullValueHandling.Ignore)]
		public int? BlockHeight { get; set; }

		/// <summary>
		/// The hash of the block including this transaction.
		/// </summary>
		[JsonProperty(PropertyName = "blockHash", NullValueHandling = NullValueHandling.Ignore)]
		[JsonConverter(typeof(UInt256JsonConverter))]
		public uint256 BlockHash { get; set; }

		/// <summary>
		/// Gets or sets the creation time.
		/// </summary>
		[JsonProperty(PropertyName = "creationTime")]
		[JsonConverter(typeof(DateTimeOffsetConverter))]
		public DateTimeOffset CreationTime { get; set; }

		/// <summary>
		/// Gets or sets the Merkle proof for this transaction.
		/// </summary>
		[JsonProperty(PropertyName = "merkleProof", NullValueHandling = NullValueHandling.Ignore)]
		[JsonConverter(typeof(BitcoinSerializableJsonConverter))]
		public PartialMerkleTree MerkleProof { get; set; }

		/// <summary>
		/// The script pub key for this address.
		/// </summary>
		[JsonProperty(PropertyName = "scriptPubKey")]
		[JsonConverter(typeof(ScriptJsonConverter))]
		public Script ScriptPubKey { get; set; }

		/// <summary>
		/// Hexadecimal representation of this transaction.
		/// </summary>
		[JsonProperty(PropertyName = "hex", NullValueHandling = NullValueHandling.Ignore)]
		public string Hex { get; set; }

		/// <summary>
		/// Propagation state of this transaction.
		/// </summary>
		/// <remarks>Assume it's <c>true</c> if the field is <c>null</c>.</remarks>
		[JsonProperty(PropertyName = "isPropagated", NullValueHandling = NullValueHandling.Ignore)]
		public bool? IsPropagated { get; set; }

		/// <summary>
		/// Gets or sets the full transaction object.
		/// </summary>
		[JsonIgnore]
		public Transaction Transaction => Transaction.Parse(this.Hex);

		/// <summary>
		/// The details of the transaction in which the output referenced in this transaction is spent.
		/// </summary>
		[JsonProperty(PropertyName = "spendingDetails", NullValueHandling = NullValueHandling.Ignore)]
		public SpendingDetails SpendingDetails { get; set; }

		/// <summary>
		/// Determines whether this transaction is confirmed.
		/// </summary>
		public bool IsConfirmed()
		{
			return this.BlockHeight != null;
		}

		/// <summary>
		/// Indicates an output is spendable.
		/// </summary>
		public bool IsSpendable()
		{
			// TODO: Coinbase maturity check?
			return this.SpendingDetails == null;
		}

		public Money SpendableAmount(bool confirmedOnly)
		{
			// This method only returns a UTXO that has no spending output.
			// If a spending output exists (even if its not confirmed) this will return as zero balance.
			if (this.IsSpendable())
			{
				// If the 'confirmedOnly' flag is set check that the UTXO is confirmed.
				if (confirmedOnly && !this.IsConfirmed())
				{
					return Money.Zero;
				}

				return this.Amount;
			}

			return Money.Zero;
		}
	}

	/// <summary>
	/// An object representing a payment.
	/// </summary>
	public class PaymentDetails
	{
		/// <summary>
		/// The script pub key of the destination address.
		/// </summary>
		[JsonProperty(PropertyName = "destinationScriptPubKey")]
		[JsonConverter(typeof(ScriptJsonConverter))]
		public Script DestinationScriptPubKey { get; set; }

		/// <summary>
		/// The Base58 representation of the destination  address.
		/// </summary>
		[JsonProperty(PropertyName = "destinationAddress")]
		public string DestinationAddress { get; set; }

		/// <summary>
		/// The transaction amount.
		/// </summary>
		[JsonProperty(PropertyName = "amount")]
		[JsonConverter(typeof(MoneyJsonConverter))]
		public Money Amount { get; set; }
	}

	public class SpendingDetails
	{
		public SpendingDetails()
		{
			this.Payments = new List<PaymentDetails>();
		}

		/// <summary>
		/// The id of the transaction in which the output referenced in this transaction is spent.
		/// </summary>
		[JsonProperty(PropertyName = "transactionId", NullValueHandling = NullValueHandling.Ignore)]
		[JsonConverter(typeof(UInt256JsonConverter))]
		public uint256 TransactionId { get; set; }

		/// <summary>
		/// A list of payments made out in this transaction.
		/// </summary>
		[JsonProperty(PropertyName = "payments", NullValueHandling = NullValueHandling.Ignore)]
		public ICollection<PaymentDetails> Payments { get; set; }

		/// <summary>
		/// The height of the block including this transaction.
		/// </summary>
		[JsonProperty(PropertyName = "blockHeight", NullValueHandling = NullValueHandling.Ignore)]
		public int? BlockHeight { get; set; }

		/// <summary>
		/// A value indicating whether this is a coin stake transaction or not.
		/// </summary>
		[JsonProperty(PropertyName = "isCoinStake", NullValueHandling = NullValueHandling.Ignore)]
		public bool? IsCoinStake { get; set; }

		/// <summary>
		/// Gets or sets the creation time.
		/// </summary>
		[JsonProperty(PropertyName = "creationTime")]
		[JsonConverter(typeof(DateTimeOffsetConverter))]
		public DateTimeOffset CreationTime { get; set; }

		/// <summary>
		/// Hexadecimal representation of this spending transaction.
		/// </summary>
		[JsonProperty(PropertyName = "hex", NullValueHandling = NullValueHandling.Ignore)]
		public string Hex { get; set; }

		/// <summary>
		/// Gets or sets the full transaction object.
		/// </summary>
		[JsonIgnore]
		public Transaction Transaction => Transaction.Parse(this.Hex);

		/// <summary>
		/// Determines whether this transaction being spent is confirmed.
		/// </summary>
		public bool IsSpentConfirmed()
		{
			return this.BlockHeight != null;
		}
	}

	/// <summary>
	/// Represents an UTXO that keeps a reference to <see cref="GeneralPurposeAddress"/> and <see cref="GeneralPurposeAccount"/>.
	/// </summary>
	public class UnspentOutputReference
	{
		/// <summary>
		/// The transaction representing the UTXO.
		/// </summary>
		public TransactionData Transaction { get; set; }

		/// <summary>
		/// Convert the <see cref="TransactionData"/> to an <see cref="OutPoint"/>
		/// </summary>
		/// <returns>The corresponding <see cref="OutPoint"/>.</returns>
		public OutPoint ToOutPoint()
		{
			return new OutPoint(this.Transaction.Id, (uint)this.Transaction.Index);
		}
	}
}
