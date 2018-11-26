﻿using System.ComponentModel.DataAnnotations;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Utilities.JsonConverters;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.Models
{
    /// <summary>
    /// Block tip Hash and Height model.
    /// </summary>
    public class BlockTipModel : RequestModel, IBlockTip
    {
        public BlockTipModel(uint256 hash, int height, int matureConfirmations)
        {
            this.Hash = hash;
            this.Height = height;
            this.MatureConfirmations = matureConfirmations;
        }

        [Required(ErrorMessage = "Block Hash is required")]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 Hash { get; set; }

        [Required(ErrorMessage = "Block Height is required")]
        public int Height { get; set; }

        [Required(ErrorMessage = "Mature Confirmations is required")]
        public int MatureConfirmations { get; set; }
    }
}
