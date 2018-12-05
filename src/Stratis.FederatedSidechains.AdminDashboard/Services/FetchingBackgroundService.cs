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
            await BuildCacheAsync();

            await updaterHub.Clients.All.SendAsync("AnotherUselessAction");

            //TODO: Add timer setting in configuration file
            this.dataRetrieverTimer = new Timer(DoWorkAsync, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
            await Task.CompletedTask;
        }

        private async Task BuildCacheAsync()
        {
            #region Stratis Node
            var stratisGetStatus = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/Node/status");
            var stratisGetRawmempool = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/Mempool/getrawmempool");
            dynamic stratisStatus = JsonConvert.DeserializeObject(stratisGetStatus.Content);
            dynamic stratisRawmempool = JsonConvert.DeserializeObject(stratisGetRawmempool.Content);
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
                    SyncingStatus = 50,
                    Peers = stratisStatus.outboundPeers,
                    BlockHash = "ebfc5fcd96e25ac2969acc84c20ca7b2e940240694e7fa3ec92d6041fe603ed9",
                    BlockHeight = stratisStatus.blockStoreHeight,
                    MempoolSize = stratisRawmempool.Count
                },  
                SidechainNode = new SidechainNodelModel
                {
                    WebAPIUrl = string.Concat(this.defaultEndpointsSettings.SidechainNode, "/api"),
                    SwaggerUrl = string.Concat(this.defaultEndpointsSettings.SidechainNode, "/swagger"),
                    SyncingStatus = 70,
                    Peers = sidechainStatus.outboundPeers,
                    BlockHash = "ebfc5fcd96e25ac2969acc84c20ca7b2e940240694e7fa3ec92d6041fe603ed9",
                    BlockHeight = sidechainStatus.blockStoreHeight,
                    MempoolSize = sidechainRawmempool.Count
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