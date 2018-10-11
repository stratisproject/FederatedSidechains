using System.ComponentModel.DataAnnotations;
using Stratis.Bitcoin.Features.Wallet.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.Models
{
    /// <summary>
    /// Model for the "enablefederation" request.
    /// </summary>
    public class EnableFederationRequest : RequestModel
    {
        /// <summary>
        /// The federation wallet password.
        /// </summary>
        [Required(ErrorMessage = "A password is required.")]
        public string Password { get; set; }
    }
}