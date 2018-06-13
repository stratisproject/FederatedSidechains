using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Utilities.ValidationAttributes;

namespace Stratis.Bitcoin.Features.GeneralPurposeWallet.Models
{
    public class BuildTransactionRequest : TxFeeEstimateRequest
    {
        [MoneyFormat(isRequired: false, ErrorMessage = "The fee is not in the correct format.")]
        public string FeeAmount { get; set; }

        [Required(ErrorMessage = "A password is required.")]
        public string Password { get; set; }

        public byte[] OpReturnData { get; set; }
    }
}
