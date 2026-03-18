using Microsoft.AspNetCore.SignalR;

namespace PersonIdentificationSystem.API.Hubs;

public class DetectionHub : Hub
{
    private readonly ILogger<DetectionHub> _logger;

    public DetectionHub(ILogger<DetectionHub> logger)
    {
        _logger = logger;
    }

    public async Task JoinStreamGroup(string streamId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"stream-{streamId}");
        _logger.LogInformation("Client {ConnectionId} joined stream group {StreamId}", Context.ConnectionId, streamId);
    }

    public async Task LeaveStreamGroup(string streamId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"stream-{streamId}");
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("SignalR client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("SignalR client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
