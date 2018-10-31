using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.Hubs
{
    public class PullMaturedBlockHub : Hub<IPullMaturedBlockClient>
    {
        private ILogger logger;
        private const string MaturedBlockProvidersGroup = "MaturedBlockProviders";

        public PullMaturedBlockHub(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public async Task SendAllMaturedBlockSinceLastKnownToConsumers(MaturedBlockModel lastKnownMaturedBlock)
        {
            try
            {
                var maturedBlocks = await this.Clients.Group(MaturedBlockProvidersGroup)
                    .SendAllMaturedBlockSinceLastKnown(lastKnownMaturedBlock);
            }
            catch (Exception exception)
            {
                this.logger.LogWarning(exception.Message);
            }
        }

        public override async Task OnConnectedAsync()
        {
            await this.Groups.AddToGroupAsync(this.Context.ConnectionId, MaturedBlockProvidersGroup);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await this.Groups.RemoveFromGroupAsync(Context.ConnectionId, MaturedBlockProvidersGroup);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
