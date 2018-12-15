using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.Models
{
    /// <summary>
    /// An instance of this class represents a particular block hash and associated height on the source chain.
    /// </summary>
    public class AuthorizeWithdrawalsModel : RequestModel, IAuthorizeWithdrawalsModel
    {
        [Required(ErrorMessage = "An array of transactions is required")]
        public string[] TransactionHex { get; set; }
    }
}
