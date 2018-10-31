using Microsoft.AspNetCore.Mvc;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface IFederationWalletController
    {
        IActionResult GetGeneralInfo();

        IActionResult GetBalance();

        /// <summary>
        /// Starts sending block to the wallet for synchronisation.
        /// This is for demo and testing use only.
        /// </summary>
        /// <param name="model">The hash of the block from which to start syncing.</param>
        IActionResult Sync([FromBody] HashModel model);

        /// <summary>
        /// Imports the federation member's mnemonic key.
        /// </summary>
        /// <param name="request">The object containing the parameters used to recover a wallet.</param>
        IActionResult ImportMemberKey([FromBody]ImportMemberKeyRequest request);

        /// <summary>
        /// Provide the federation wallet's credentials so that it can sign transactions.
        /// </summary>
        /// <param name="request">The password of the federation wallet.</param>
        /// <returns>An <see cref="OkResult"/> object that produces a status code 200 HTTP response.</returns>
        IActionResult EnableFederation([FromBody]EnableFederationRequest request);
    }
}