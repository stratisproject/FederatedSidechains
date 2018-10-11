using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.Models
{
    /// <summary>
    /// Helper class to interpret a string as json.
    /// </summary>
    public class JsonContent : StringContent
    {
        public JsonContent(object obj) :
            base(JsonConvert.SerializeObject(obj), Encoding.UTF8, "application/json")
        {

        }
    }

    /// <summary>
    /// Used to create a session that builds a multi-sig transaction by requesting
    /// signing from other Federation nodes and then broadcasts the transaction.
    /// </summary>
    public class CreateTargetTransferRequest : RequestModel
    {
        public List<TargetTransferRequest> CounterChainTransactionInfos { get; set; }

        /// <summary>
        /// Number of the block at which the countersession was initiated
        /// </summary>
        [Required(ErrorMessage = "BlockHeight needs to be specified.")]
        [Range(0, int.MaxValue, ErrorMessage = "Invalid BlockHeight")]
        public int BlockHeight { get; set; }
    }
}
