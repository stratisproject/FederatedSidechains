﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using Stratis.Bitcoin.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;
using Stratis.FederatedPeg.Features.FederationGateway.NetworkHelpers;

namespace Stratis.FederatedPeg.Features.FederationGateway.MonitorChain
{
    /// <summary>
    /// The MonitorChainSessionManager creates a session that is used to communicate transaction info
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
    /// Gateways monitor the Mainchain for deposit transactions and the Sidechain for withdrawals.
    /// Deposits are an exact mirror image of withdrawals and the exact same process (and code) is used.
    /// We have therefore, a MonitorChain and a CounterChain. For a deposit, the MonitorChain is
    /// Mainchain and the CounterChain is our Sidechain. For withdrawals the MonitorChain is the
    /// Sidechain and the CounterChain is Mainchain.
    /// </summary>
    public class MonitorChainSessionManager : IMonitorChainSessionManager
    {
        // The number of blocks to wait before we create the session.
        // This is typically used for reorgs protection.
        private const int BlockSecurityDelay = 0;

        // The time between sessions.
        private TimeSpan sessionRunInterval = new TimeSpan(hours: 0, minutes: 0, seconds: 30);

        // The logger.
        private readonly ILogger logger;

        // Timer used to trigger session processing.
        private Timer actionTimer;
        
        // The network we are running.
        private Network network;

        // Settings from the config files. 
        private FederationGatewaySettings federationGatewaySettings;

        // Our monitor sessions.
        private ConcurrentDictionary<uint256, MonitorChainSession> monitorSessions = new ConcurrentDictionary<uint256, MonitorChainSession>();

        // The IBD state.
        private IInitialBlockDownloadState initialBlockDownloadState;

        // A locker object.
        private object locker = new object();

        // The blockchain.
        private ConcurrentChain concurrentChain;

        // The auditor can capture the details of the transactions that the monitor discovers.
        private ICrossChainTransactionAuditor crossChainTransactionAuditor;

        public MonitorChainSessionManager(
            ILoggerFactory loggerFactory,
            FederationGatewaySettings federationGatewaySettings,
            Network network,
            ConcurrentChain concurrentChain,
            IInitialBlockDownloadState initialBlockDownloadState,
            ICrossChainTransactionAuditor crossChainTransactionAuditor = null)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.federationGatewaySettings = federationGatewaySettings;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.concurrentChain = concurrentChain;
            this.crossChainTransactionAuditor = crossChainTransactionAuditor;
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

        // Creates the Monitor session.
        // A session is added when the CrossChainTransactionMonitor identifies a transaction that needs to be completed cross chain.
        public void CreateMonitorSession(CrossChainTransactionInfo crossChainTransactionInfo)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(crossChainTransactionInfo.CrossChainTransactionId), crossChainTransactionInfo.CrossChainTransactionId, nameof(crossChainTransactionInfo.DestinationAddress), crossChainTransactionInfo.DestinationAddress);

            var monitorChainSession = new MonitorChainSession(
                DateTime.Now,
                crossChainTransactionInfo.TransactionHash,
                crossChainTransactionInfo.Amount,
                crossChainTransactionInfo.DestinationAddress,
                crossChainTransactionInfo.BlockNumber,
                this.network.ToChain(),
                this.federationGatewaySettings.FederationPublicKeys.Select(f => f.ToHex()).ToArray(),
                this.federationGatewaySettings.PublicKey,
                this.federationGatewaySettings.MultiSigM,
                this.federationGatewaySettings.MultiSigN
            );

            this.monitorSessions.TryAdd(monitorChainSession.SessionId, monitorChainSession);
            this.logger.LogInformation($"MonitorChainSession added: {monitorChainSession}");
            
            // Call to the counter chain and tell it to also create a session.
            this.CreateSessionOnCounterChain(this.federationGatewaySettings.CounterChainApiPort,
                crossChainTransactionInfo.TransactionHash,
                crossChainTransactionInfo.Amount,
                crossChainTransactionInfo.DestinationAddress);

            this.logger.LogTrace("(-)");
        }

        // Calls into the counter chain and registers the session there.
        private void CreateSessionOnCounterChain(int apiPortForSidechain, uint256 transactionId, Money amount, string destination)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}',{4}:'{5}',{6}:'{7}')", nameof(apiPortForSidechain), apiPortForSidechain, nameof(transactionId), transactionId, nameof(amount), amount, nameof(destination), destination);

            var createCounterChainSessionRequest = new CreateCounterChainSessionRequest
            {
                SessionId = transactionId,
                Amount = amount.ToString(),
                DestinationAddress = destination
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

            // We don't process sessions if our chain is not past IBD.
            if (this.initialBlockDownloadState.IsInitialBlockDownload())
            {
                this.logger.LogInformation($"RunSessionAsync() Monitor chain is in IBD exiting. Height:{this.concurrentChain.Height}.");
                return;
            }

            foreach (var monitorChainSession in this.monitorSessions.Values)
            {
                // Don't start the session if we are still in the SecurityDelay
                if (!IsBeyondBlockSecurityDelay(monitorChainSession)) continue;

                var time = DateTime.Now;

                this.logger.LogInformation($"RunSessionAsync() MyBossCard:{monitorChainSession.BossCard}");
                this.logger.LogInformation($"At {time} AmITheBoss: {monitorChainSession.AmITheBoss(time)} WhoHoldsTheBossCard: {monitorChainSession.WhoHoldsTheBossCard(time)}");

                if (monitorChainSession.Status == SessionStatus.Created && monitorChainSession.AmITheBoss(time))
                {
                    monitorChainSession.Status = SessionStatus.Requesting;

                    this.logger.LogInformation($"RunSessionAsync() MyBossCard:{monitorChainSession.BossCard}");
                    this.logger.LogInformation($"At {time} AmITheBoss: {monitorChainSession.AmITheBoss(time)} WhoHoldsTheBossCard: {monitorChainSession.WhoHoldsTheBossCard(time)}");

                    // We can keep sending this session until we get a result.
                    var result = await ProcessSessionOnCounterChain(
                        this.federationGatewaySettings.CounterChainApiPort,
                        monitorChainSession.Amount,
                        monitorChainSession.DestinationAddress,
                        monitorChainSession.SessionId).ConfigureAwait(false);

                    monitorChainSession.Status = SessionStatus.Requested;

                    if (result != uint256.Zero)
                    {
                        monitorChainSession.Complete(result);
                        this.logger.LogInformation($"RunSessionAsync() - Completing Session {result}.");
                        this.crossChainTransactionAuditor.AddCounterChainTransactionId(monitorChainSession.SessionId, result);
                    }
                }
            }
        }

        private bool IsBeyondBlockSecurityDelay(MonitorChainSession monitorChainSession)
        {
            this.logger.LogInformation("IsBeyondBlockSecurityDelay()");
            this.logger.LogInformation($"IsBeyondBlockSecurityDelay() SessionBlock: {monitorChainSession.BlockNumber}");
            this.logger.LogInformation($"IsBeyondBlockSecurityDelay() BlockDelay: {BlockSecurityDelay}");
            this.logger.LogInformation($"IsBeyondBlockSecurityDelay() Height: {this.concurrentChain.Tip.Height}");

            return this.concurrentChain.Tip.Height >= (monitorChainSession.BlockNumber + BlockSecurityDelay);
        }

        // Calls into the counter chain and sets off the process to build the multi-sig transaction.
        private async Task<uint256> ProcessSessionOnCounterChain(int apiPortForSidechain, Money amount, string destination, uint256 transactionId)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}',{4}:'{5}',{6}:'{7}')", nameof(apiPortForSidechain), apiPortForSidechain, nameof(transactionId), transactionId, nameof(amount), amount, nameof(destination), destination);

            var createPartialTransactionSessionRequest = new CreateCounterChainSessionRequest
            {
                SessionId = transactionId,
                Amount = amount.ToString(),
                DestinationAddress = destination
            };

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var uri = new Uri($"http://localhost:{apiPortForSidechain}/api/FederationGateway/process-session-oncounterchain");
                var request = new JsonContent(createPartialTransactionSessionRequest);
                
                try
                {
                    var httpResponseMessage = await client.PostAsync(uri, request);
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
