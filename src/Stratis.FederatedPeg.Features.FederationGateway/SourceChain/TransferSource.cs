using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.NetworkHelpers;
using Stratis.FederatedPeg.Features.FederationGateway.TargetChain;

namespace Stratis.FederatedPeg.Features.FederationGateway.SourceChain
{
    ///<inheritdoc/>
    internal class TransferSource : ITransferSource
    {
        private readonly ILogger logger;

        // Our session manager.
        private readonly ITransferSourceManager sourceChainSessionManager;

        private readonly ITargetTransferManager counterChainSessionManager;

        private readonly FederationGatewaySettings federationGatewaySettings;

        private readonly Network network;

        private readonly ConcurrentChain concurrentChain;

        private readonly IInitialBlockDownloadState initialBlockDownloadState;

        private readonly ITransferAuditor crossChainTransactionAuditor;

        // The minimum transfer amount permissible.

        // TODO: Initially added to prevent spamming of the network,
        // but this needs to be changed to minimum fee, someone can still spam the network sending back
        // and forth big amounts to their own addresses.
        private readonly Money minimumTransferAmount = new Money(1.0m, MoneyUnit.BTC);

        // The redeem Script we are monitoring.
        private Script script;

        public TransferSource(
            ILoggerFactory loggerFactory,
            Network network,
            ConcurrentChain concurrentChain,
            FederationGatewaySettings federationGatewaySettings,
            ITransferSourceManager sourceChainSessionManager,
            ITargetTransferManager counterChainSessionManager,
            IInitialBlockDownloadState initialBlockDownloadState,
            ITransferAuditor crossChainTransactionAuditor = null)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(sourceChainSessionManager, nameof(sourceChainSessionManager));
            Guard.NotNull(counterChainSessionManager, nameof(counterChainSessionManager));
            Guard.NotNull(federationGatewaySettings, nameof(federationGatewaySettings));
            Guard.NotNull(concurrentChain, nameof(concurrentChain));
            Guard.NotNull(initialBlockDownloadState, nameof(initialBlockDownloadState));
            Guard.NotNull(crossChainTransactionAuditor, nameof(crossChainTransactionAuditor));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.sourceChainSessionManager = sourceChainSessionManager;
            this.federationGatewaySettings = federationGatewaySettings;
            this.concurrentChain = concurrentChain;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.crossChainTransactionAuditor = crossChainTransactionAuditor;
            this.counterChainSessionManager = counterChainSessionManager;
        }

        /// <inheritdoc />
        /// <summary>
        /// Saves the store during shutdown.
        /// </summary>
        public void Dispose()
        {
            this.crossChainTransactionAuditor.Dispose();
        }

        /// <inheritdoc/>>
        public void Initialize(FederationGatewaySettings federationGatewaySettings)
        {
            // Read the relevant multisig address with help of the folder manager.
            this.script = this.federationGatewaySettings.MultiSigAddress.ScriptPubKey;

            // Load the auditor if present.
            this.crossChainTransactionAuditor.Initialize();
        }

        /// <inheritdoc/>>
        public void ProcessBlock(Block block)
        {
            Guard.NotNull(block, nameof(block));
            this.logger.LogTrace("({0}:'{1}')", nameof(block.GetHash), block.GetHash());

            ChainedHeader newTip = this.concurrentChain.GetBlock(block.GetHash());
            if (newTip == null)
            {
                this.logger.LogTrace("(-)[NEW_TIP_REORG]");
                return;
            }

            var chainBlockTip = this.concurrentChain.GetBlock(block.GetHash());
            int blockNumber = chainBlockTip.Height;

            if (this.initialBlockDownloadState.IsInitialBlockDownload())
            {
                this.logger.LogTrace("MonitorChain ({0}) in IBD: blockNumber {1} not processed.", this.network.ToChain(), blockNumber);
                return;
            }

            this.logger.LogTrace("Monitor Processing Block: {0} on {1}", blockNumber, this.network.ToChain());

            // Create a session to process the transaction.
            // Tell our Session Manager that we can start a new session.
            TransferLeaderSelector monitorSession = new TransferLeaderSelector(blockNumber, this.federationGatewaySettings.FederationPublicKeys.Select(f => f.ToHex()).ToArray(), this.federationGatewaySettings.PublicKey);

            foreach (var transaction in block.Transactions)
            {
                foreach (var txOut in transaction.Outputs)
                {
                    if (txOut.ScriptPubKey != this.script) continue;
                    var stringResult = OpReturnDataReader.GetStringFromOpReturn(this.logger, this.network, transaction, out var opReturnDataType);

                    switch (opReturnDataType)
                    {
                        case OpReturnDataType.Unknown:
                            this.logger.LogTrace("Received transaction with unknown OP_RETURN data: {0}. Transaction hash: {1}.", stringResult, transaction.GetHash());
                            continue;
                        case OpReturnDataType.Address:
                            this.logger.LogInformation("Processing received transaction with address: {0}. Transaction hash: {1}.", stringResult, transaction.GetHash());
                            Transfer trxInfo = this.ProcessAddress(transaction.GetHash(), stringResult, txOut.Value, blockNumber, block.GetHash());

                            if (trxInfo != null)
                            {
                                this.crossChainTransactionAuditor.AddTransferInfo(trxInfo);

                                // Commit audit as we know we have a new record. 
                                this.crossChainTransactionAuditor.Commit();

                                monitorSession.Transfers.Add(trxInfo);
                            }
                            continue;
                        case OpReturnDataType.BlockHeight:
                            var blockHeight = int.Parse(stringResult);
                            this.logger.LogInformation("AddCounterChainTransactionId: {0} for session in block {1}.", transaction.GetHash(), blockHeight);
                            this.counterChainSessionManager.AddCounterChainTransactionId(blockHeight, transaction.GetHash());
                            continue;
                        case OpReturnDataType.Hash:
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }

            if (!monitorSession.Transfers.Any()) return;

            this.logger.LogInformation("AddTargetTransactionId: Found {0} transactions to process in block with height {1}.", monitorSession.Transfers.Count, monitorSession.BlockNumber);
            this.sourceChainSessionManager.Register(monitorSession);
            this.sourceChainSessionManager.CreateSessionOnCounterChain(this.federationGatewaySettings.CounterChainApiPort, monitorSession);
        }

        private Transfer ProcessAddress(uint256 sourceTransactionId, string targetAddress, Money amount, int sourceBlockNumber, uint256 blockHash)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}',{4}:'{5}')", nameof(sourceTransactionId), sourceTransactionId, nameof(amount), amount, nameof(targetAddress), targetAddress, nameof(sourceBlockNumber), sourceBlockNumber, nameof(blockHash), blockHash);

            if (amount < this.minimumTransferAmount)
            {
                this.logger.LogInformation($"The transaction {sourceTransactionId} has less than the MinimumTransferAmount.  Ignoring. ");
                return null;
            }

            var transfer = new Transfer
            {
                TargetAddress = targetAddress,
                SourceDepositAmount = amount,
                SourceBlockNumber = sourceBlockNumber,
                SourceBlockHash = blockHash,
                SourceTransactionId = sourceTransactionId
            };

            this.logger.LogInformation("Crosschain Transaction Found on : {0}", this.network.ToChain());
            this.logger.LogInformation("Crosschain transfer details: {0}", transfer);
            return transfer;
        }
    }
}