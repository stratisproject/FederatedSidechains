using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stratis.FederatedSidechains.AdminDashboard.Settings;
using RestSharp;

namespace Stratis.FederatedSidechains.AdminDashboard.Controllers
{
    public class ApexNodeController : Controller
    {
        public ApexNodeController()
        {
        }

        [Route("resync")]
        public IActionResult Resync(int height)
        {
            return Ok();
        }

        [Route("resync-crosschain-transactions")]
        public IActionResult ResyncCrosschainTransactions()
        {
            return Ok();
        }

        [Route("stop")]
        public IActionResult StopNode()
        {
            return Ok();
        }
    }
}
