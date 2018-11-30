using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stratis.FederatedSidechains.AdminDashboard.Settings;
using Stratis.FederatedSidechains.AdminDashboard.Rest;
using RestSharp;
using Stratis.FederatedSidechains.AdminDashboard.Filters;

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
        public IActionResult EnableFederation(string mnemonic, string password)
        {
            return Ok();
        }

        [Ajax]
        [HttpPost]
        [Route("resync")]
        public async Task<IActionResult> ResyncAsync(int height)
        {
            return Ok();
        }

        [Ajax]
        [Route("resync-crosschain-transactions")]
        public IActionResult ResyncCrosschainTransactions()
        {
            //await ApiRequester.GetRequestAsync("/api/Node/status");
            return Ok();
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
