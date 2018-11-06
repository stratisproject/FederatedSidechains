using System.ComponentModel.DataAnnotations;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.Models
{
    /// <summary>
    /// ADD SUMMARY
    /// </summary>
    public class BlockTipModel : RequestModel, IBlockTip
    {
        public BlockTipModel(string hash, int height)
        {
            this.Hash = hash;
            this.Height = height;
        }

        [Required(ErrorMessage = "Block Hash is required")]
        public string Hash { get; }

        [Required(ErrorMessage = "Block Height is required")]
        public int Height { get; }
    }
}
