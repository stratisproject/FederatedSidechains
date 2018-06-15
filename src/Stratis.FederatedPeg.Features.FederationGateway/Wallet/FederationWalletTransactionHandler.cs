﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Policy;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.Wallet
{
    /// <summary>
    /// A handler that has various functionalities related to transaction operations.
    /// </summary>
    /// <remarks>
    /// This will uses the <see cref="IWalletFeePolicy"/> and the <see cref="TransactionBuilder"/>.
    /// TODO: Move also the broadcast transaction to this class
    /// TODO: Implement lockUnspents
    /// TODO: Implement subtractFeeFromOutputs
    /// </remarks>
    public class FederationWalletTransactionHandler : IFederationWalletTransactionHandler
    {
        /// <summary>A threshold that if possible will limit the amount of UTXO sent to the <see cref="ICoinSelector"/>.</summary>
        /// <remarks>
        /// 500 is a safe number that if reached ensures the coin selector will not take too long to complete,
        /// most regular wallets will never reach such a high number of UTXO.
        /// </remarks>
        private const int SendCountThresholdLimit = 500;

        private readonly IFederationWalletManager walletManager;

        private readonly IWalletFeePolicy walletFeePolicy;

        private readonly CoinType coinType;

        private readonly ILogger logger;

	    private readonly Network network;

        public FederationWalletTransactionHandler(
            ILoggerFactory loggerFactory,
            IFederationWalletManager walletManager,
            IWalletFeePolicy walletFeePolicy,
            Network network)
        {
            this.walletManager = walletManager;
            this.walletFeePolicy = walletFeePolicy;
	        this.network = network;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public Transaction BuildTransaction(TransactionBuildContext context)
        {
            this.InitializeTransactionBuilder(context);

            if (context.Shuffle)
            {
                context.TransactionBuilder.Shuffle();
            }

            // build transaction
            context.Transaction = context.TransactionBuilder.BuildTransaction(context.Sign);

			// If this is a multisig transaction, then by definition we only (usually) possess one of the keys
			// and can therefore not immediately construct a transaction that passes verification
	        if (!context.IgnoreVerify)
	        {
		        if (!context.TransactionBuilder.Verify(context.Transaction, out TransactionPolicyError[] errors))
		        {
			        string errorsMessage = string.Join(" - ", errors.Select(s => s.ToString()));
			        this.logger.LogError($"Build transaction failed: {errorsMessage}");
			        throw new WalletException($"Could not build the transaction. Details: {errorsMessage}");
		        }
	        }

	        return context.Transaction;
        }

        /// <inheritdoc />
        public void FundTransaction(TransactionBuildContext context, Transaction transaction)
        {
            if (context.Recipients.Any())
                throw new WalletException("Adding outputs is not allowed.");

            // Turn the txout set into a Recipient array
            context.Recipients.AddRange(transaction.Outputs
                .Select(s => new Recipient
                {
                    ScriptPubKey = s.ScriptPubKey,
                    Amount = s.Value,
                    SubtractFeeFromAmount = false // default for now
                }));

            context.AllowOtherInputs = true;

            foreach (var transactionInput in transaction.Inputs)
                context.SelectedInputs.Add(transactionInput.PrevOut);

            var newTransaction = this.BuildTransaction(context);

            if (context.ChangeAddress != null)
            {
                // find the position of the change and move it over.
                var index = 0;
                foreach (var newTransactionOutput in newTransaction.Outputs)
                {
                    if (newTransactionOutput.ScriptPubKey == context.ChangeAddress.ScriptPubKey)
                    {
                        transaction.Outputs.Insert(index, newTransactionOutput);
                    }

                    index++;
                }
            }

            // TODO: copy the new output amount size (this also includes spreading the fee over all outputs)

            // copy all the inputs from the new transaction.
            foreach (var newTransactionInput in newTransaction.Inputs)
            {
                if (!context.SelectedInputs.Contains(newTransactionInput.PrevOut))
                {
                    transaction.Inputs.Add(newTransactionInput);

                    // TODO: build a mechanism to lock inputs
                }
            }
        }

        /// <inheritdoc />
        public Money EstimateFee(Wallet.TransactionBuildContext context)
        {
            this.InitializeTransactionBuilder(context);

            return context.TransactionFee;
        }

        /// <summary>
        /// Initializes the context transaction builder from information in <see cref="TransactionBuildContext"/>.
        /// </summary>
        /// <param name="context">Transaction build context.</param>
        private void InitializeTransactionBuilder(Wallet.TransactionBuildContext context)
        {
            Guard.NotNull(context, nameof(context));
            Guard.NotNull(context.Recipients, nameof(context.Recipients));
            Guard.NotNull(context.AccountReference, nameof(context.AccountReference));

            context.TransactionBuilder = new TransactionBuilder(this.network);

            this.AddRecipients(context);
            this.AddOpReturnOutput(context);
            this.AddCoins(context);
            this.AddSecrets(context);
            this.FindChangeAddress(context);
            this.AddFee(context);
        }

        /// <summary>
        /// Loads the private key for the multisig address.
        /// </summary>
        /// <param name="context">The context associated with the current transaction being built.</param>
        private void AddSecrets(TransactionBuildContext context)
        {
            if (!context.Sign)
                return;

            FederationWallet wallet = this.walletManager.GetWallet();
	        var signingKeys = new HashSet<ISecret>();

			//foreach (var unspentOutputsItem in context.UnspentOutputs)
			//{
			//	var privKey = address.GetPrivateKey(wallet.EncryptedSeed, context.WalletPassword, wallet.Network);
			//	var secret = new BitcoinSecret(privKey, wallet.Network);
			//	signingKeys.Add(secret);
			//	added.Add(unspentOutputsItem.Address);
			//}
			
	        context.TransactionBuilder.AddKeys(signingKeys.ToArray());
        }

        /// <summary>
        /// Find the next available change address.
        /// </summary>
        /// <param name="context">The context associated with the current transaction being built.</param>
        private void FindChangeAddress(TransactionBuildContext context)
        {
            // get address to send the change to
            context.ChangeAddress = this.walletManager.GetWallet().MultiSigAddress;
            context.TransactionBuilder.SetChange(context.ChangeAddress.ScriptPubKey);
        }

		/// <summary>
		/// Find all available outputs (UTXO's) that belong to <see cref="WalletAccountReference.AccountName"/>.
		/// Then add them to the <see cref="TransactionBuildContext.UnspentOutputs"/> or <see cref="TransactionBuildContext.UnspentMultiSigOutputs"/>.
		/// </summary>
		/// <param name="context">The context associated with the current transaction being built.</param>
		private void AddCoins(TransactionBuildContext context)
        {
		    context.UnspentOutputs = this.walletManager.GetSpendableTransactionsInWallet(context.MinConfirmations).ToList();

		    if (context.UnspentOutputs.Count == 0)
		    {
			    throw new WalletException("No spendable transactions found.");
		    }

		    // Get total spendable balance in the account.
		    var balance = context.UnspentOutputs.Sum(t => t.Transaction.Amount);
		    var totalToSend = context.Recipients.Sum(s => s.Amount);
		    if (balance < totalToSend)
			    throw new WalletException("Not enough funds.");

		    if (context.SelectedInputs.Any())
		    {
			    // 'SelectedInputs' are inputs that must be included in the
			    // current transaction. At this point we check the given
			    // input is part of the UTXO set and filter out UTXOs that are not
			    // in the initial list if 'context.AllowOtherInputs' is false.

			    var availableHashList = context.UnspentOutputs.ToDictionary(item => item.ToOutPoint(), item => item);

			    if (!context.SelectedInputs.All(input => availableHashList.ContainsKey(input)))
				    throw new WalletException("Not all the selected inputs were found on the wallet.");

			    if (!context.AllowOtherInputs)
			    {
				    foreach (var unspentOutputsItem in availableHashList)
					    if (!context.SelectedInputs.Contains(unspentOutputsItem.Key))
						    context.UnspentOutputs.Remove(unspentOutputsItem.Value);
			    }
		    }

		    Money sum = 0;
		    int index = 0;
		    var coins = new List<Coin>();
		    foreach (var item in context.UnspentOutputs.OrderByDescending(a => a.Transaction.Amount))
		    {
                // TODO 
				//coins.Add(ScriptCoin.Create(this.network, item.Transaction.Id, (uint)item.Transaction.Index, item.Transaction.Amount, item.Transaction.ScriptPubKey, item.Address.RedeemScript));
			    sum += item.Transaction.Amount;
			    index++;

			    // If threshold is reached and the total value is above the target
			    // then its safe to stop adding UTXOs to the coin list.
			    // The primary goal is to reduce the time it takes to build a trx
			    // when the wallet is bloated with UTXOs.
			    if (index > SendCountThresholdLimit && sum > totalToSend)
				    break;
		    }

		    // All the UTXOs are added to the builder without filtering.
		    // The builder then has its own coin selection mechanism
		    // to select the best UTXO set for the corresponding amount.
		    // To add a custom implementation of a coin selection override
		    // the builder using builder.SetCoinSelection().

		    context.TransactionBuilder.AddCoins(coins);
        }

        /// <summary>
        /// Add recipients to the <see cref="TransactionBuilder"/>.
        /// </summary>
        /// <param name="context">The context associated with the current transaction being built.</param>
        /// <remarks>
        /// Add outputs to the <see cref="TransactionBuilder"/> based on the <see cref="Recipient"/> list.
        /// </remarks>
        private void AddRecipients(TransactionBuildContext context)
        {
            if (context.Recipients.Any(a => a.Amount == Money.Zero))
                throw new WalletException("No amount specified.");

            if (context.Recipients.Any(a => a.SubtractFeeFromAmount))
                throw new NotImplementedException("Substracting the fee from the recipient is not supported yet.");

            foreach (var recipient in context.Recipients)
                context.TransactionBuilder.Send(recipient.ScriptPubKey, recipient.Amount);
        }

        /// <summary>
        /// Use the <see cref="FeeRate"/> from the <see cref="walletFeePolicy"/>.
        /// </summary>
        /// <param name="context">The context associated with the current transaction being built.</param>
        private void AddFee(TransactionBuildContext context)
        {
            Money fee;

            // If the fee hasn't been set manually, calculate it based on the fee type that was chosen.
            if (context.TransactionFee == null)
            {
                FeeRate feeRate = context.OverrideFeeRate ?? this.walletFeePolicy.GetFeeRate(context.FeeType.ToConfirmations());
                fee = context.TransactionBuilder.EstimateFees(feeRate);
            }
            else
            {
                fee = context.TransactionFee;
            }

            context.TransactionBuilder.SendFees(fee);
            context.TransactionFee = fee;
        }

        /// <summary>
        /// Add extra unspendable output to the transaction if there is anything in OpReturnData.
        /// </summary>
        /// <param name="context">The context associated with the current transaction being built.</param>
        private void AddOpReturnOutput(TransactionBuildContext context)
        {
            if (context.OpReturnData == null) return;

            var opReturnScript = TxNullDataTemplate.Instance.GenerateScriptPubKey(Encoding.UTF8.GetBytes(context.OpReturnData));
            context.TransactionBuilder.Send(opReturnScript, Money.Zero);
        }
    }

    public class TransactionBuildContext
    {
        /// <summary>
        /// Initialize a new instance of a <see cref="TransactionBuildContext"/>
        /// </summary>
        /// <param name="accountReference">The wallet and account from which to build this transaction</param>
        /// <param name="recipients">The target recipients to send coins to.</param>
        public TransactionBuildContext(WalletAccountReference accountReference, List<Recipient> recipients)
            : this(accountReference, recipients, string.Empty)
        {
        }

        /// <summary>
        /// Initialize a new instance of a <see cref="TransactionBuildContext"/>
        /// </summary>
        /// <param name="accountReference">The wallet and account from which to build this transaction</param>
        /// <param name="recipients">The target recipients to send coins to.</param>
        /// <param name="walletPassword">The password that protects the wallet in <see cref="accountReference"/></param>
        public TransactionBuildContext(WalletAccountReference accountReference, List<Recipient> recipients, string walletPassword = "", string opReturnData = null)
        {
            Guard.NotNull(recipients, nameof(recipients));

            this.AccountReference = accountReference;
            this.Recipients = recipients;
            this.WalletPassword = walletPassword;
            this.FeeType = FeeType.Medium;
            this.MinConfirmations = 1;
            this.SelectedInputs = new List<OutPoint>();
            this.AllowOtherInputs = false;
            this.Sign = !string.IsNullOrEmpty(walletPassword);
            this.OpReturnData = opReturnData;
	        this.MultiSig = null;
	        this.IgnoreVerify = false;
        }

        /// <summary>
        /// The wallet account to use for building a transaction
        /// </summary>
        public WalletAccountReference AccountReference { get; set; }

        /// <summary>
        /// The recipients to send Bitcoin to.
        /// </summary>
        public List<Recipient> Recipients { get; set; }

        /// <summary>
        /// An indicator to estimate how much fee to spend on a transaction.
        /// </summary>
        /// <remarks>
        /// The higher the fee the faster a transaction will get in to a block.
        /// </remarks>
        public FeeType FeeType { get; set; }

        /// <summary>
        /// The minimum number of confirmations an output must have to be included as an input.
        /// </summary>
        public int MinConfirmations { get; set; }

        /// <summary>
        /// Coins that are available to be spent.
        /// </summary>
        public List<Wallet.UnspentOutputReference> UnspentOutputs { get; set; }

		public Network Network { get; set; }

		/// <summary>
		/// The builder used to build the current transaction.
		/// </summary>
		public TransactionBuilder TransactionBuilder { get; set; }

        /// <summary>
        /// The change address, where any remaining funds will be sent to.
        /// </summary>
        /// <remarks>
        /// A Bitcoin has to spend the entire UTXO, if total value is greater then the send target
        /// the rest of the coins go in to a change address that is under the senders control.
        /// </remarks>
        public MultiSigAddress ChangeAddress { get; set; }

        /// <summary>
        /// The total fee on the transaction.
        /// </summary>
        public Money TransactionFee { get; set; }

        /// <summary>
        /// The final transaction.
        /// </summary>
        public Transaction Transaction { get; set; }

        /// <summary>
        /// The password that protects the wallet in <see cref="WalletAccountReference"/>.
        /// </summary>
        /// <remarks>
        /// TODO: replace this with System.Security.SecureString (https://github.com/dotnet/corefx/tree/master/src/System.Security.SecureString)
        /// More info (https://github.com/dotnet/corefx/issues/1387)
        /// </remarks>
        public string WalletPassword { get; set; }

        /// <summary>
        /// The inputs that must be used when building the transaction.
        /// </summary>
        /// <remarks>
        /// The inputs are required to be part of the wallet.
        /// </remarks>
        public List<OutPoint> SelectedInputs { get; set; }

        /// <summary>
        /// If false, allows unselected inputs, but requires all selected inputs be used
        /// </summary>
        public bool AllowOtherInputs { get; set; }

        /// <summary>
        /// Specify whether to sign the transaction.
        /// </summary>
        public bool Sign { get; set; }

        /// <summary>
        /// Allows the context to specify a <see cref="FeeRate"/> when building a transaction.
        /// </summary>
        public FeeRate OverrideFeeRate { get; set; }

        /// <summary>
        /// Shuffles transaction inputs and outputs for increased privacy.
        /// </summary>
        public bool Shuffle { get; set; }

        /// <summary>
        /// Optional data to be added as an extra OP_RETURN transaction output with Money.Zero value.
        /// </summary>
        public string OpReturnData { get; set; }

        /// <summary>
        /// If not null, indicates the multisig address details that funds can be sourced from.
        /// </summary>
        public MultiSigAddress MultiSig { get; set; }

	    /// <summary>
	    /// If true, do not perform verification on the built transaction (e.g. it is partially signed)
	    /// </summary>
	    public bool IgnoreVerify { get; set; }
	}

    /// <summary>
    /// Represents recipients of a payment, used in <see cref="FederationWalletTransactionHandler.BuildTransaction"/>
    /// </summary>
    public class Recipient
    {
        /// <summary>
        /// The destination script.
        /// </summary>
        public Script ScriptPubKey { get; set; }

        /// <summary>
        /// The amount that will be sent.
        /// </summary>
        public Money Amount { get; set; }

        /// <summary>
        /// An indicator if the fee is subtracted from the current recipient.
        /// </summary>
        public bool SubtractFeeFromAmount { get; set; }
    }
}
