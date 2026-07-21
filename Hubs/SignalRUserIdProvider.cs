using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace WebAndMobile.Hubs;

public class SignalRUserIdProvider : IUserIdProvider
{
    public string? GetUserId(
        HubConnectionContext connection)
    {
        return connection.User?
            .FindFirstValue(
                ClaimTypes.NameIdentifier
            );
    }
}