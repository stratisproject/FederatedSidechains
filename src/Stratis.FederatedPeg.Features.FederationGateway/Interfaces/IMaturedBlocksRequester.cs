using System.Threading.Tasks;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface IMaturedBlocksRequester
    {
        /// <summary>
        /// Sets the counter-chain tip height.
        /// </summary>
        /// <param name="tipHeight">The height of a received chain A block.</param>
        void SetTip(int tipHeight);

        /// <summary>
        /// Get the last received deposit height and possible triggers the synchornization.
        /// </summary>
        /// <param name="lastReceived">The height of a received chain A block.</param>
        void SetLastReceived(int lastReceived);

        /// <summary>
        /// Starts the requester.
        /// </summary>
        void Start();
    }
}