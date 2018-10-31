using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.Hubs
{
    public class PushMaturedBlockHub : Hub<IPushMaturedBlockClient>
    {
        private ILogger logger;
        private const string MaturedBlockConsumersGroup = "MaturedBlockConsumers";

        public PushMaturedBlockHub(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public async Task SendMaturedBlockDepositsToConsumers(MaturedBlockDepositsModel maturedBlockDeposits)
        {
            try
            {
                await this.Clients.Group(MaturedBlockConsumersGroup)
                    .ReceiveMaturedBlockDeposits(maturedBlockDeposits);
            }
            catch (Exception exception)
            {
                this.logger.LogWarning(exception.Message);
            }
        }

        public override async Task OnConnectedAsync()
        {
            await this.Groups.AddToGroupAsync(this.Context.ConnectionId, MaturedBlockConsumersGroup);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await this.Groups.RemoveFromGroupAsync(this.Context.ConnectionId, MaturedBlockConsumersGroup);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
