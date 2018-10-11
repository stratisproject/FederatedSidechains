using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities.JsonConverters;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.SourceChain
{
    /// <summary>
    /// The SourceChainSessionManager creates a session that is used to communicate transaction info
    /// to the CounterChain.  When a federation member is asked to sign a transaction, the details of
    /// that transaction (amount and destination address) are checked against this session data. 
    /// 
    /// A federation member runs full node versions of both chains that trust each other and communicate
    /// through a local API.  However, other federation members are not immediately trusted and requests
    /// to sign a partial transaction are checked for validity before the transaction is signed.  
    /// This means checking:
    ///   a) That a session exists (prevents a rouge gateway from generating fake transactions).
    ///   b) That the address matches (prevents a rouge gateway from diverting funds).
    ///   c) That the amount matches (enforces exactly matching of debits and credits across the two chains).
    ///   d) It is also necessary to check that a federation member has not already signed the transaction.
    ///      The federation gateway ensures that transactions are only ever signed once. (If a rouge
    ///      federation gateway circulates multiple transaction templates with difference spending inputs this
    ///      rule ensures that these are not signed.)
    /// 
    /// Both nodes need to be fully synced before any processing is done and nodes only process cross chain transactions from 
    /// new blocks. They never look backwards to do any corrective processing of transactions that may have failed. It is assumed
    /// that the other gateways have reached a quorum on those transactions. Should that have not happened then corrective action
    /// involves an offline agreement between nodes to post any corrective measures. Processing is never done if either node is
    /// in initialBlockDownload.
    /// 
    /// Gateways monitor the source chain for deposit transactions and the target for withdrawals.
    /// </summary>
    public class TransferSourceManager : ITransferSourceManager
    {
        // The number of blocks to wait before we create the session.
        // This is typically used for reorgs protection.
        private const int BlockSecurityDelay = 0;

        private readonly ILogger logger;

        // The time between transfers.
        private readonly TimeSpan sessionRunInterval = new TimeSpan(hours: 0, minutes: 0, seconds: 30);

        // Timer used to trigger session processing.
        private Timer actionTimer;

        private readonly FederationGatewaySettings federationGatewaySettings;

        private readonly ConcurrentDictionary<int, TransferLeaderSelector> sourceTransfers = new ConcurrentDictionary<int, TransferLeaderSelector>();

        private readonly IInitialBlockDownloadState initialBlockDownloadState;

        private readonly ConcurrentChain concurrentChain;

        // The auditor can capture the details of the transactions that the monitor discovers.
        private readonly ITransferAuditor transferAuditor;

        public TransferSourceManager(
            ILoggerFactory loggerFactory,
            FederationGatewaySettings federationGatewaySettings,
            ConcurrentChain concurrentChain,
            IInitialBlockDownloadState initialBlockDownloadState,
            ITransferAuditor transferAuditor = null)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.federationGatewaySettings = federationGatewaySettings;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.concurrentChain = concurrentChain;
            this.transferAuditor = transferAuditor;
        }

        public void Initialize()
        {
            // We don't know how regular blocks will be on the sidechain so instead of
            // processing sessions on new blocks we use a timer.
            this.actionTimer = new Timer(async (o) =>
            {
                await this.RunSessionAsync().ConfigureAwait(false);
            }, null, 0, (int)this.sessionRunInterval.TotalMilliseconds);
        }

        public void Dispose()
        {
            this.actionTimer?.Dispose();
        }

        // A session is added when the CrossChainTransactionMonitor identifies a transaction that needs to be completed cross chain.
        public void Register(TransferLeaderSelector transferSource)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(transferSource.BlockNumber), transferSource.BlockNumber);

            this.sourceTransfers.TryAdd(transferSource.BlockNumber, transferSource);
        }

        // Calls into the counter chain and registers the session there.
        public void CreateSessionOnCounterChain(int apiPortForSidechain, TransferLeaderSelector monitorChainSession)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}',{4}:'{5}')", nameof(apiPortForSidechain), apiPortForSidechain, nameof(monitorChainSession.BlockNumber), monitorChainSession.BlockNumber, "Transactions Count", monitorChainSession.Transfers.Count);

            var createCounterChainSessionRequest = new CreateTargetTransferRequest
            {
                BlockHeight = monitorChainSession.BlockNumber,
                CounterChainTransactionInfos = monitorChainSession.Transfers.Select(t => new TargetTransferRequest()
                {
                    TransactionId = t.SourceTransactionId,
                    TargetAddress = t.TargetAddress,
                    Amount = t.SourceDepositAmount.ToString()
                }).ToList()
            };

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var uri = new Uri($"http://localhost:{apiPortForSidechain}/api/FederationGateway/create-session-oncounterchain");
                var request = new JsonContent(createCounterChainSessionRequest);

                try
                {
                    var httpResponseMessage = client.PostAsync(uri, request).ConfigureAwait(false).GetAwaiter().GetResult();
                    string json = httpResponseMessage.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                }
                catch (Exception e)
                {
                    this.logger.LogError(e, "Error occurred when calling /api/FederationGateway/create-session-oncounterchain: {0}", e.Message);
                }
            }

            this.logger.LogTrace("(-)");
        }

        private async Task RunSessionAsync()
        {
            this.logger.LogTrace("()");
            this.logger.LogInformation("RunSessionAsync()");

            if (this.initialBlockDownloadState.IsInitialBlockDownload())
            {
                this.logger.LogInformation("RunSessionAsync() Monitor chain is in IBD exiting. Height:{0}.", this.concurrentChain.Height);
                return;
            }

            foreach (var sourceTransfer in this.sourceTransfers.Values)
            {
                if (sourceTransfer.Status.RequiresResend()) continue;

                if (!IsBeyondBlockSecurityDelay(sourceTransfer)) continue;

                var time = DateTime.Now;

                this.logger.LogInformation($"Session status: {0} with CounterChainTransactionId: {1}.",
                    sourceTransfer.Status.ToString(), sourceTransfer.TargetTransactionId);

                this.logger.LogInformation("RunSessionAsync() MyBossCard: {0}", sourceTransfer.BossCard);
                this.logger.LogInformation("At {0} AmITheBoss: {1} WhoHoldsTheBossCard: {2}",
                    time, sourceTransfer.AmITheBoss(time), sourceTransfer.WhoHoldsTheBossCard(time));

                if (!sourceTransfer.AmITheBoss(time) &&
                    sourceTransfer.Status != Transfer.Status.PendingSignatures) continue;

                if (sourceTransfer.Status == Transfer.Status.Created)
                    // Session was newly created and I'm the boss so I start the process.
                    sourceTransfer.Status = Transfer.Status.PendingSignatures;

                // We call this for an incomplete session whenever we become the boss.
                // On the target chain this will either start the process or just inform us
                // the the session completed already (and give us the CounterChainTransactionId).
                // We we were already the boss the status will be Requested and we will Process
                // to get the CounterChainTransactionId.
                var result = await ProcessTransferTargetAsync(this.federationGatewaySettings.CounterChainApiPort, sourceTransfer)
                                 .ConfigureAwait(false);

                if (sourceTransfer.Status != Transfer.Status.Signed)
                    sourceTransfer.Status = Transfer.Status.PendingSignatures;

                if (result != uint256.Zero)
                {
                    sourceTransfer.Complete(result);

                    if (result == uint256.One) this.logger.LogInformation("Session for block {0} failed.", sourceTransfer.BlockNumber);
                    else this.logger.LogInformation("RunSessionAsync() - Completing Session {0}.", result);

                    foreach (var trx in sourceTransfer.Transfers)
                    {
                        this.transferAuditor.AddTargetTransactionId(trx.SourceTransactionId, result);
                    }
                    this.transferAuditor.Commit();
                }

                this.logger.LogInformation("Status: {0} with result: {1}.", sourceTransfer.Status, result);
            }
        }

        private bool IsBeyondBlockSecurityDelay(TransferLeaderSelector monitorChainSession)
        {
            this.logger.LogInformation("() Session started at block {0}, block security delay {1}, current block height {2}, waiting for {3} more blocks",
                monitorChainSession.BlockNumber, BlockSecurityDelay, this.concurrentChain.Tip.Height,
                monitorChainSession.BlockNumber + BlockSecurityDelay - this.concurrentChain.Tip.Height);

            return this.concurrentChain.Tip.Height >= (monitorChainSession.BlockNumber + BlockSecurityDelay);
        }

        // Calls into the counter chain and sets off the process to build the multi-sig transaction.
        private async Task<uint256> ProcessTransferTargetAsync(int apiPortForSidechain, TransferLeaderSelector monitorChainSession)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}',{4}:'{5}')", nameof(apiPortForSidechain), apiPortForSidechain, nameof(monitorChainSession.BlockNumber), monitorChainSession.BlockNumber, "Transactions Count", monitorChainSession.Transfers.Count);

            var createCounterChainSessionRequest = new CreateTargetTransferRequest
            {
                BlockHeight = monitorChainSession.BlockNumber,
                CounterChainTransactionInfos = monitorChainSession.Transfers.Select(t => new TargetTransferRequest()
                {
                    TransactionId = t.SourceTransactionId,
                    TargetAddress = t.TargetAddress,
                    Amount = t.SourceDepositAmount.ToString()
                }).ToList()
            };

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var uri = new Uri($"http://localhost:{apiPortForSidechain}/api/FederationGateway/process-session-oncounterchain");
                var request = new JsonContent(createCounterChainSessionRequest);

                try
                {
                    var httpResponseMessage = await client.PostAsync(uri, request);
                    this.logger.LogInformation("Response: {0}", await httpResponseMessage.Content.ReadAsStringAsync());

                    if (httpResponseMessage.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return uint256.One;
                    }

                    if (httpResponseMessage.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        this.logger.LogError("Error occurred when calling /api/FederationGateway/process-session-oncounterchain: {0}-{1}", httpResponseMessage.StatusCode, httpResponseMessage.ToString());
                        return uint256.Zero;
                    }

                    string json = await httpResponseMessage.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<uint256>(json, new UInt256JsonConverter());
                }
                catch (Exception e)
                {
                    this.logger.LogError(e, "Error occurred when calling /api/FederationGateway/process-session-oncounterchain: {0}", e.Message);
                    return uint256.Zero;
                }
            }
        }
    }
}

