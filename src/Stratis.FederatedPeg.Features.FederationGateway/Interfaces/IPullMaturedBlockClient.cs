using System.Collections.Generic;
using System.Threading.Tasks;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    /// <summary>
    /// In this hub, the target node is the server and calls method on source node to request for information about the source
    /// chain as it requires them. This is the case for instance when a re-sync is needed upon (re)starting a target node.
    /// </summary>
    public interface IPullMaturedBlockClient
    {
        /// <summary>
        /// This method is meant to be called by the target node, on the source node instance, to get all matured blocks
        /// that appeared after a given block, for instance when a re-sync is needed.
        /// </summary>
        /// <returns>All the deposits from blocks that matured after the <see cref="lastKnownMaturedBlock"/></returns>>
        Task<List<MaturedBlockDepositsModel>> SendAllMaturedBlockSinceLastKnown(MaturedBlockModel lastKnownMaturedBlock);
    }
}