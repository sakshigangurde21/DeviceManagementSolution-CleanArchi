using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace Infrastructure.Hubs
{
    [Authorize(Roles = "Admin,User")]
    public class DeviceHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var username = Context.User?.Identity?.Name;
            Console.WriteLine($"SignalR connected: {username}");
            await base.OnConnectedAsync();
        }
    }
}