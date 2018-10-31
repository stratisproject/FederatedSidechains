using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Utilities.JsonConverters;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.Models
{
    /// <summary>
    /// When a block matures, an instance of this class is created and passed on to the target chain.
    /// If there are no deposits, we still need to send an empty list with corresponding block height
    /// and hash so that the target node knows that block has been seen and dealt with.
    /// </summary>
    public class MaturedBlockDepositsModel : RequestModel
    {
        [Required(ErrorMessage = "A list of deposits is required")]
        public IList<IDeposit> Deposits { get; set; }

        [Required(ErrorMessage = "A block hash is required")]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint BlockHash { get; set; }

        [Required(ErrorMessage = "A block height is required")]
        public int BlockHeight { get; set; }
    }
}
