using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stratis.FederatedSidechains.AdminDashboard.Settings;
using RestSharp;
using System.Threading;
using Microsoft.AspNetCore.SignalR;
using Stratis.FederatedSidechains.AdminDashboard.Hubs;
using Stratis.FederatedSidechains.AdminDashboard.Filters;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using Stratis.FederatedSidechains.AdminDashboard.Models;
using System;

namespace Stratis.FederatedSidechains.AdminDashboard.Controllers
{
    public class HomeController : Controller
    {
        private readonly FullNodeSettings fullNodeSettings;
        private readonly IDistributedCache distributedCache;
        public readonly IHubContext<DataUpdaterHub> updaterHub;

        public IOptions<FullNodeSettings> FullNodeSettings { get; }

        public HomeController(IDistributedCache distributedCache, IOptions<FullNodeSettings> fullNodeSettings, IHubContext<DataUpdaterHub> hubContext)
        {
            this.fullNodeSettings = fullNodeSettings.Value;
            this.distributedCache = distributedCache;
            this.updaterHub = hubContext;
            FullNodeSettings = fullNodeSettings;
        }
        
        [Ajax]
        [Route("check-federation")]
        public IActionResult CheckFederation()
        {
            //TODO: detect if federation is enabled
            return Json(false);
        }

        public IActionResult Index()
        {
            // Checking if the local cache is built otherwise we will display the initialization page
            if(string.IsNullOrEmpty(this.distributedCache.GetString("DashboardData")))
            {
                ViewBag.NodeUnavailable = !string.IsNullOrEmpty(this.distributedCache.GetString("NodeUnavailable"));
                return View("Initialization");
            }

            var dashboardModel = JsonConvert.DeserializeObject<DashboardModel>(this.distributedCache.GetString("DashboardData"));
            return View("Dashboard", dashboardModel);
        }
    }
}
