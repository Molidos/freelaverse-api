using Microsoft.AspNetCore.SignalR;

namespace Freelaverse.API.Hubs;

public class PaymentsHub : Hub
{
    public Task JoinUserGroup(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return Task.CompletedTask;
        return Groups.AddToGroupAsync(Context.ConnectionId, GetGroup(email));
    }

    public Task LeaveUserGroup(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return Task.CompletedTask;
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, GetGroup(email));
    }

    public static string GetGroup(string email) => $"user:{email.Trim().ToLowerInvariant()}";
}
