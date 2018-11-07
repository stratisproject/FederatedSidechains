using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.FederatedPeg.Features.FederationGateway.Controllers;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.SourceChain
{
    public class RestBlockTipSender : RestSenderBase, IBlockTipSender
    {
        public RestBlockTipSender(ILoggerFactory loggerFactory, IFederationGatewaySettings settings)
            : base(loggerFactory, settings)
        {
        }

        public async Task SendBlockTipAsync(IBlockTip blockTip)
        {
            await this.SendAsync(blockTip, FederationGatewayController.ReceiveCurrentBlockTipRoute).ConfigureAwait(false);
        }
    }
}
