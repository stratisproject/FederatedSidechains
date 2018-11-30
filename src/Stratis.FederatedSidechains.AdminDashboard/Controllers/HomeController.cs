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
            return View("Dashboard");
        }
    }
}
