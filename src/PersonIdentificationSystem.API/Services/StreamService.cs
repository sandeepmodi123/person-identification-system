using PersonIdentificationSystem.API.DTOs;
using PersonIdentificationSystem.API.Models.Entities;
using PersonIdentificationSystem.API.Repositories;

namespace PersonIdentificationSystem.API.Services;

public interface IStreamService
{
    Task<List<RTSPStreamDto>> GetAllAsync(CancellationToken ct = default);
    Task<RTSPStreamDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<RTSPStreamDto> CreateAsync(CreateRTSPStreamRequest request, CancellationToken ct = default);
    Task<RTSPStreamDto?> UpdateAsync(Guid id, UpdateRTSPStreamRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<StreamConnectionTestResult> TestConnectionAsync(Guid id, CancellationToken ct = default);
}

public class StreamService : IStreamService
{
    private readonly IStreamRepository _streamRepo;
    private readonly ILogger<StreamService> _logger;

    public StreamService(IStreamRepository streamRepo, ILogger<StreamService> logger)
    {
        _streamRepo = streamRepo;
        _logger = logger;
    }

    public async Task<List<RTSPStreamDto>> GetAllAsync(CancellationToken ct = default)
    {
        var streams = await _streamRepo.GetAllAsync(ct);
        return streams.Select(MapToDto).ToList();
    }

    public async Task<RTSPStreamDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var stream = await _streamRepo.GetByIdAsync(id, ct);
        return stream is null ? null : MapToDto(stream);
    }

    public async Task<RTSPStreamDto> CreateAsync(CreateRTSPStreamRequest request, CancellationToken ct = default)
    {
        var stream = new RTSPStream
        {
            CameraName = request.CameraName.Trim(),
            CameraLocation = request.CameraLocation?.Trim(),
            RtspUrl = request.RtspUrl.Trim(),
            FrameIntervalSeconds = request.FrameIntervalSeconds,
            IsActive = request.IsActive
        };
        await _streamRepo.AddAsync(stream, ct);
        _logger.LogInformation("Created RTSP stream {StreamId} - {CameraName}", stream.Id, stream.CameraName);
        return MapToDto(stream);
    }

    public async Task<RTSPStreamDto?> UpdateAsync(Guid id, UpdateRTSPStreamRequest request, CancellationToken ct = default)
    {
        var stream = await _streamRepo.GetByIdAsync(id, ct);
        if (stream is null) return null;

        if (request.CameraName is not null) stream.CameraName = request.CameraName.Trim();
        if (request.CameraLocation is not null) stream.CameraLocation = request.CameraLocation.Trim();
        if (request.RtspUrl is not null) stream.RtspUrl = request.RtspUrl.Trim();
        if (request.FrameIntervalSeconds.HasValue) stream.FrameIntervalSeconds = request.FrameIntervalSeconds.Value;
        if (request.IsActive.HasValue) stream.IsActive = request.IsActive.Value;
        stream.DateUpdated = DateTime.UtcNow;

        await _streamRepo.UpdateAsync(stream, ct);
        return MapToDto(stream);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var stream = await _streamRepo.GetByIdAsync(id, ct);
        if (stream is null) return false;
        await _streamRepo.DeleteAsync(stream, ct);
        return true;
    }

    public async Task<StreamConnectionTestResult> TestConnectionAsync(Guid id, CancellationToken ct = default)
    {
        var stream = await _streamRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Stream {id} not found.");

        var start = DateTime.UtcNow;
        // In production: attempt RTSP handshake. For POC, we ping the host.
        bool isReachable = false;
        string? errorMessage = null;
        int? latencyMs = null;

        try
        {
            var uri = new Uri(stream.RtspUrl.Replace("rtsp://", "http://"));
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var sw = System.Diagnostics.Stopwatch.StartNew();
            // Attempt TCP connection to host:port
            using var tcpClient = new System.Net.Sockets.TcpClient();
            await tcpClient.ConnectAsync(uri.Host, uri.Port > 0 ? uri.Port : 554, ct);
            sw.Stop();
            isReachable = true;
            latencyMs = (int)sw.ElapsedMilliseconds;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }

        stream.Status = isReachable ? "Online" : "Offline";
        stream.LastChecked = DateTime.UtcNow;
        await _streamRepo.UpdateAsync(stream, ct);

        return new StreamConnectionTestResult(id, isReachable, latencyMs, start, errorMessage);
    }

    private static RTSPStreamDto MapToDto(RTSPStream s) => new(
        s.Id, s.CameraName, s.CameraLocation, s.RtspUrl,
        s.FrameIntervalSeconds, s.IsActive, s.Status, s.LastChecked);
}
