using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Stratis.FederatedSidechains.AdminDashboard
{
    public class DataRetrieverService : IHostedService, IDisposable
    {
        private Timer _serviceTimer;

        public DataRetrieverService()
        {
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            //TODO: Add timer setting in configuration file
            _serviceTimer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
            return Task.CompletedTask;
        }
        private void DoWork(object state)
        {
            Console.WriteLine("ok");
        }
            
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _serviceTimer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _serviceTimer?.Dispose();
        }
    }
}