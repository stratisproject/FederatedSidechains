using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stratis.FederatedSidechains.AdminDashboard.Settings;
using RestSharp;

namespace Stratis.FederatedSidechains.AdminDashboard.Controllers
{
    public class HomeController : Controller
    {
        private readonly FullNodeSettings fullNodeSettings;

        public HomeController(IOptions<FullNodeSettings> fullNodeSettings)
        {
            this.fullNodeSettings = fullNodeSettings.Value;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> LetsGoAsync()
        {
            var rc = new RestClient($"{this.fullNodeSettings.Endpoint}/api/Node/status");
            var rr = new RestRequest(Method.GET);
            IRestResponse response = await rc.ExecuteTaskAsync(rr);
            return Content(response.Content);
        }
    }
}
