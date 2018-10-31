using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.Controllers
{
    /// <summary>
    /// API used to communicate across to the counter chain.
    /// </summary>
    public interface IFederationGatewayController
    {
        /// <summary>
        /// Our deposit and withdrawal transactions start on mainchain and sidechain respectively. Two transactions are used, one on each chain, to complete
        /// the 'movement'.
        /// This API call informs the counterchain node that this session exists.  All the federation nodes monitoring the blockchain will ask
        /// their counterchains so register the session.  The boss counterchain will use this session to process the transaction whereas the other nodes
        /// will use this session information to Verify that the transaction is valid.
        /// </summary>
        /// <param name="createCounterChainSessionRequest">Used to pass the SessionId, Amount and Destination address to the counter chain.</param>
        /// <returns>An ActionResult.</returns>
        IActionResult CreateSessionOnCounterChain([FromBody] CreateCounterChainSessionRequest createCounterChainSessionRequest);

        /// <summary>
        /// The session boss asks his counterchain node to go ahead with broadcasting the partial template, wait for replies, then build and broadcast
        /// the counterchain transaction. (Other federation counterchain nodes will not do this unless they later become the boss however the non-boss 
        /// counterchain nodes will know about the session already and can verify the transaction against their session info.)
        /// <param name="createCounterChainSessionRequest">Used to pass the SessionId, Amount and Destination address to the counter chain.</param>
        /// <returns>An ActionResult.</returns>
        Task<IActionResult> ProcessSessionOnCounterChain([FromBody] CreateCounterChainSessionRequest createCounterChainSessionRequest);
    }
}