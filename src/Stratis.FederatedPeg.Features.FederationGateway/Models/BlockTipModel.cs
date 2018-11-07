using System.ComponentModel.DataAnnotations;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.Models
{
    /// <summary>
    /// Block tip Hash and Height model.
    /// </summary>
    public class BlockTipModel : RequestModel, IBlockTip
    {
        public BlockTipModel(string hash, int height)
        {
            this.Hash = hash;
            this.Height = height;
        }

        public string Hash { get; }

        public int Height { get; }
    }
}
