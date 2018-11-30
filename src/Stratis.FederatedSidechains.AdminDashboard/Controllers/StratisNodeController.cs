using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stratis.FederatedSidechains.AdminDashboard.Settings;
using Stratis.FederatedSidechains.AdminDashboard.Rest;
using Stratis.FederatedSidechains.AdminDashboard.Filters;
using RestSharp;

namespace Stratis.FederatedSidechains.AdminDashboard.Controllers
{
    [Route("stratis-node")]
    public class StratisNodeController : Controller
    {
        private readonly DefaultEndpointsSettings defaultEndpointsSettings;

        public StratisNodeController(IOptions<DefaultEndpointsSettings> defaultEndpointsSettings)
        {
            this.defaultEndpointsSettings = defaultEndpointsSettings.Value;
        }

        [Ajax]
        [Route("resync")]
        public IActionResult Resync(int height)
        {
            return Ok();
        }

        [Ajax]
        [Route("resync-crosschain-transactions")]
        public IActionResult ResyncCrosschainTransactions()
        {
            return Ok();
        }

        [Ajax]
        [Route("stop")]
        public async Task<IActionResult> StopNodeAsync()
        {
            ApiResponse stopNodeRequest = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/Node/status");
            return stopNodeRequest.IsSuccess ? (IActionResult) Ok() : BadRequest();
        }
    }
}
