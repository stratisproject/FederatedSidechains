using System.Threading.Tasks;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    /// <summary>
    /// In this hub, the source node is the server and calls method on target node to allow it to receive new matured blocks content
    /// as they appear on the source chain.
    /// </summary>
    public interface IPushMaturedBlockClient
    {
        /// <summary>
        /// This method is meant to be called by the source node, on the target node instance, upon observing a new matured block
        /// in order to transmit its content.
        /// </summary>
        Task ReceiveMaturedBlockDeposits(MaturedBlockDepositsModel maturedBlockDeposits);
    }
}