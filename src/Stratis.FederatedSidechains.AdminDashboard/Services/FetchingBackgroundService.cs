using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Stratis.FederatedSidechains.AdminDashboard.Rest;
using Stratis.FederatedSidechains.AdminDashboard.Settings;
using Newtonsoft.Json;
using Stratis.FederatedSidechains.AdminDashboard.Models;
using Microsoft.AspNetCore.SignalR;
using Stratis.FederatedSidechains.AdminDashboard.Hubs;
using Microsoft.Extensions.Caching.Distributed;
using System.Net.Sockets;

namespace Stratis.FederatedSidechains.AdminDashboard.Services
{
    public class FetchingBackgroundService : IHostedService, IDisposable
    {
        private readonly DefaultEndpointsSettings defaultEndpointsSettings;
        private readonly IDistributedCache distributedCache;
        public readonly IHubContext<DataUpdaterHub> updaterHub;
        private Timer dataRetrieverTimer;

        public FetchingBackgroundService(IDistributedCache distributedCache, IOptions<DefaultEndpointsSettings> defaultEndpointsSettings, IHubContext<DataUpdaterHub> hubContext)
        {
            this.defaultEndpointsSettings = defaultEndpointsSettings.Value;
            this.distributedCache = distributedCache;
            updaterHub = hubContext;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if(PerformNodeCheck())
            {
                await this.BuildCacheAsync();
            }
            else
            {
                await this.distributedCache.SetStringAsync("NodeUnavailable", "true");
            }

            await this.updaterHub.Clients.All.SendAsync("AnotherUselessAction");

            //TODO: Add timer setting in configuration file
            this.dataRetrieverTimer = new Timer(DoWorkAsync, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
            await Task.CompletedTask;
        }

        /// <summary>
        /// Perform connection check with the nodes
        /// </summary>
        /// <remarks>The ports can be changed in the future</remarks>
        /// <returns>True if the connection are succeed</returns>
        private bool PerformNodeCheck() => this.PortCheck(37221) && this.PortCheck(38226);

        private bool PortCheck(int port)
        {
            using(TcpClient tcpClient = new TcpClient())
            {
                try
                {
                    tcpClient.Connect("127.0.0.1", port);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Retrieve all node information and store it in IDistributedCache object
        /// </summary>
        /// <returns></returns>
        private async Task BuildCacheAsync()
        {
            #region Stratis Node
            var stratisGetStatus = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/Node/status");
            var stratisGetRawmempool = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/Mempool/getrawmempool");
            var stratisGetWalletHistory = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/Wallet/history?WalletName=ClintMourlevat&AccountName=account%200");   //TODO: change wallet name
            dynamic stratisStatus = JsonConvert.DeserializeObject(stratisGetStatus.Content);
            dynamic stratisRawmempool = JsonConvert.DeserializeObject(stratisGetRawmempool.Content);
            dynamic stratisWalletHistory = JsonConvert.DeserializeObject(stratisGetWalletHistory.Content);
            #endregion

            #region Sidechain Node
            var sidechainGetStatus = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.SidechainNode, "/api/Node/status");
            var sidechainGetRawmempool = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.SidechainNode, "/api/Mempool/getrawmempool");
            dynamic sidechainStatus = JsonConvert.DeserializeObject(sidechainGetStatus.Content);
            dynamic sidechainRawmempool = JsonConvert.DeserializeObject(sidechainGetRawmempool.Content);
            #endregion

            var dashboardModel = new DashboardModel
            {
                Status = true,
                IsCacheBuilt = true,
                MainchainWalletAddress = "31EBX8oNk6GoPufm755yuFtbBgEPmjPvdK",
                SidechainWalletAddress = "pTEBX8oNk6GoPufm755yuFtbBgEPmjPvdK ",
                MiningPublicKeys = new string[] {"02446dfcecfcbef1878d906ffd023258a129e9471dba90a1845f15063397ada335", "02446dfcecfcbef1878d906ffd023258a129e9471dba90a1845f15063397ada335", "02446dfcecfcbef1878d906ffd023258a129e9471dba90a1845f15063397ada335"},
                StratisNode = new StratisNodeModel
                {
                    WebAPIUrl = string.Concat(this.defaultEndpointsSettings.StratisNode, "/api"),
                    SwaggerUrl = string.Concat(this.defaultEndpointsSettings.StratisNode, "/swagger"),
                    SyncingStatus = stratisStatus.consensusHeight > 0 ? (stratisStatus.blockStoreHeight / stratisStatus.consensusHeight) * 100 : 0,
                    Peers = stratisStatus.outboundPeers,
                    BlockHash = "ebfc5fcd96e25ac2969acc84c20ca7b2e940240694e7fa3ec92d6041fe603ed9",
                    BlockHeight = stratisStatus.blockStoreHeight,
                    MempoolSize = stratisRawmempool.Count,
                    FederationMembers = new object[] {},
                    History = stratisWalletHistory.history[0].transactionsHistory
                },  
                SidechainNode = new SidechainNodelModel
                {
                    WebAPIUrl = string.Concat(this.defaultEndpointsSettings.SidechainNode, "/api"),
                    SwaggerUrl = string.Concat(this.defaultEndpointsSettings.SidechainNode, "/swagger"),
                    SyncingStatus = sidechainStatus.consensusHeight > 0 ? (sidechainStatus.blockStoreHeight / sidechainStatus.consensusHeight) * 100 : 0,
                    Peers = sidechainStatus.outboundPeers,
                    BlockHash = "ebfc5fcd96e25ac2969acc84c20ca7b2e940240694e7fa3ec92d6041fe603ed9",
                    BlockHeight = sidechainStatus.blockStoreHeight,
                    MempoolSize = sidechainRawmempool.Count,
                    FederationMembers = new object[] {},
                    History = new object[] {}
                }
            };
                
            this.distributedCache.SetString("DashboardData", JsonConvert.SerializeObject(dashboardModel));
        }

        private async void DoWorkAsync(object state)
        {
            var status = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/Node/status");
            //Console.WriteLine(status.Content);
        }
            
        public Task StopAsync(CancellationToken cancellationToken)
        {
            this.dataRetrieverTimer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            this.dataRetrieverTimer?.Dispose();
        }
    }
}