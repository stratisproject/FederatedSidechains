using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Stratis.FederatedSidechains.AdminDashboard.Rest;
using Stratis.FederatedSidechains.AdminDashboard.Settings;

namespace Stratis.FederatedSidechains.AdminDashboard.Services
{
    public class DataRetrieverService : IHostedService, IDisposable
    {
        private readonly DefaultEndpointsSettings defaultEndpointsSettings;
        private Timer dataRetrieverTimer;

        public DataRetrieverService(IOptions<DefaultEndpointsSettings> defaultEndpointsSettings)
        {
            this.defaultEndpointsSettings = defaultEndpointsSettings.Value;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            //TODO: Add timer setting in configuration file
            this.dataRetrieverTimer = new Timer(DoWorkAsync, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
            return Task.CompletedTask;
        }

        private async void DoWorkAsync(object state)
        {
            ApiResponse status = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/Node/status");
            Console.WriteLine(status.Content);
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