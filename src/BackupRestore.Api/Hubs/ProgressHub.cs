using Microsoft.AspNetCore.SignalR;

namespace BackupRestore.Api.Hubs;

/// <summary>
/// SignalR hub that pushes live job progress to browser clients. Clients join a
/// group per job id to receive targeted updates.
/// </summary>
public class ProgressHub : Hub
{
    public Task Subscribe(string jobId)
        => Groups.AddToGroupAsync(Context.ConnectionId, jobId);

    public Task Unsubscribe(string jobId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, jobId);
}
