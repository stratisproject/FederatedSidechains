using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface IMaturedBlocksProvider
    {
        /// <summary>
        /// Retrieves deposits for the indicated blocks from the block repository and throws an error if the blocks are not mature enough.
        /// </summary>
        /// <param name="blockHeight">The block height at which to start retrieving blocks.</param>
        /// <param name="maxBlocks">The number of blocks to retrieve.</param>
        /// <returns>A list of mature block deposits.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the blocks are not mature or not found.</exception>
        Task<List<IMaturedBlockDeposits>> GetMaturedDepositsAsync(int blockHeight, int maxBlocks);

        /// <summary>
        /// Gets all the available chained headers starting at the specified block height up to a maximum number of headers.
        /// </summary>
        /// <param name="blockHeight">The block height at which to start.</param>
        /// <param name="maxHeaders">The maximum number of headers to get.</param>
        /// <returns>All the available chained headers starting at the specified block height up to the maximum number of headers.</returns>
        Task<List<ChainedHeader>> GetChainedHeadersAsync(int blockHeight, int maxHeaders);

        /// <summary>
        /// Gets deposits from the block that is expected to be mature given this chain header.
        /// </summary>
        /// <param name="chainedHeader">The last received chain header.</param>
        /// <returns>The matured deposits.</returns>
        IMaturedBlockDeposits ExtractMaturedBlockDeposits(ChainedHeader chainedHeader);
    }
}