using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace Stratis.FederatedSidechains.AdminDashboard.Hubs
{
    public class DataUpdaterHub : Hub
    {
        public async Task SendMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }
    }
}