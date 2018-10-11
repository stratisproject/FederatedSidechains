using System.ComponentModel.DataAnnotations;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.FederatedPeg.Features.FederationGateway.Models
{
    public class TargetTransferRequest : RequestModel
    {
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 TransactionId { get; set; }

        /// <summary>
        /// The amount of the transaction.
        /// </summary>
        [Required(ErrorMessage = "An amount required.")]
        public string Amount { get; set; }

        /// <summary>
        /// The final destination address of the user to receive the funds. For a deposit this is a user address on the sidechain,
        /// for a withdrawal it is an address on the mainchain.
        /// </summary>
        [Required(ErrorMessage = "Destination Address required.")]
        public string TargetAddress { get; set; }

    }
}