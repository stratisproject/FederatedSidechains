using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stratis.FederatedSidechains.AdminDashboard.Settings;
using Stratis.FederatedSidechains.AdminDashboard.Rest;
using RestSharp;
using Stratis.FederatedSidechains.AdminDashboard.Filters;
using Newtonsoft.Json;

namespace Stratis.FederatedSidechains.AdminDashboard.Controllers
{
    [Route("sidechain-node")]
    public class SidechainNodeController : Controller
    {
        private readonly DefaultEndpointsSettings defaultEndpointsSettings;

        public SidechainNodeController(IOptions<DefaultEndpointsSettings> defaultEndpointsSettings)
        {
            this.defaultEndpointsSettings = defaultEndpointsSettings.Value;
        }

        [Ajax]
        [Route("enable-federation")]
        public async Task<IActionResult> EnableFederationAsync(string mnemonic, string password)
        {
            ApiResponse importWalletRequest = await ApiRequester.PostRequestAsync(this.defaultEndpointsSettings.SidechainNode, "/api/FederationWallet/import-key", new {mnemonic, password});
            if(importWalletRequest.IsSuccess)
            {
                ApiResponse enableFederationRequest = await ApiRequester.PostRequestAsync(this.defaultEndpointsSettings.SidechainNode, "/api/FederationWallet/enable-federation", new {password});
                return enableFederationRequest.IsSuccess ? (IActionResult) Ok() : BadRequest();
            }
            return BadRequest();
        }

        [Ajax]
        [HttpPost]
        [Route("resync")]
        public async Task<IActionResult> ResyncAsync(int height)
        {
            ApiResponse stopNodeRequest = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/Node/status");
            return stopNodeRequest.IsSuccess ? (IActionResult) Ok() : BadRequest();
        }

        [Ajax]
        [Route("resync-crosschain-transactions")]
        public async Task<IActionResult> ResyncCrosschainTransactionsAsync()
        {
            ApiResponse stopNodeRequest = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/Node/status");
            return stopNodeRequest.IsSuccess ? (IActionResult) Ok() : BadRequest();
        }

        [Ajax]
        [Route("stop")]
        public async Task<IActionResult> StopNodeAsync()
        {
            ApiResponse stopNodeRequest = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.SidechainNode, "/api/Node/status");
            return stopNodeRequest.IsSuccess ? (IActionResult)Ok() : BadRequest();
        }
    }
}
