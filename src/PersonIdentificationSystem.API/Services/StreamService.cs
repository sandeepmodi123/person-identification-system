using PersonIdentificationSystem.API.DTOs;
using PersonIdentificationSystem.API.Models.Entities;
using PersonIdentificationSystem.API.Repositories;
using System.Net.Sockets;
using System.Text;

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
        bool isReachable = false;
        string? errorMessage = null;
        int? latencyMs = null;

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var uri = new Uri(stream.RtspUrl);
            var probe = await ProbeRtspDescribeAsync(uri, ct);
            sw.Stop();

            isReachable = probe.Success;
            errorMessage = probe.ErrorMessage;
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

    private static async Task<(bool Success, string? ErrorMessage)> ProbeRtspDescribeAsync(Uri uri, CancellationToken ct)
    {
        if (!string.Equals(uri.Scheme, "rtsp", StringComparison.OrdinalIgnoreCase))
        {
            return (false, $"Invalid URL scheme '{uri.Scheme}'. Expected rtsp://");
        }

        var port = uri.Port > 0 ? uri.Port : 554;

        using var tcpClient = new TcpClient();
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        connectCts.CancelAfter(TimeSpan.FromSeconds(6));
        await tcpClient.ConnectAsync(uri.Host, port, connectCts.Token);

        using var networkStream = tcpClient.GetStream();
        networkStream.ReadTimeout = 6000;
        networkStream.WriteTimeout = 6000;

        var target = uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.PathAndQuery, UriFormat.UriEscaped);
        var requestBuilder = new StringBuilder();
        requestBuilder.Append($"DESCRIBE {target} RTSP/1.0\r\n");
        requestBuilder.Append("CSeq: 1\r\n");
        requestBuilder.Append("Accept: application/sdp\r\n");
        requestBuilder.Append("User-Agent: PersonIdentificationSystem.API/1.0\r\n");

        if (!string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes(uri.UserInfo));
            requestBuilder.Append($"Authorization: Basic {auth}\r\n");
        }

        requestBuilder.Append("\r\n");
        var requestBytes = Encoding.ASCII.GetBytes(requestBuilder.ToString());
        await networkStream.WriteAsync(requestBytes, ct);

        var buffer = new byte[4096];
        var read = await networkStream.ReadAsync(buffer, ct);
        if (read <= 0)
        {
            return (false, "No RTSP response received from camera.");
        }

        var response = Encoding.ASCII.GetString(buffer, 0, read);
        var firstLine = response.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)[0];

        if (!firstLine.StartsWith("RTSP/", StringComparison.OrdinalIgnoreCase))
        {
            return (false, $"Unexpected RTSP response: {firstLine}");
        }

        var parts = firstLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !int.TryParse(parts[1], out var statusCode))
        {
            return (false, $"Could not parse RTSP status line: {firstLine}");
        }

        if (statusCode >= 200 && statusCode < 300)
        {
            return (true, null);
        }

        return (false, $"RTSP DESCRIBE failed with status {statusCode}: {firstLine}");
    }

    private static RTSPStreamDto MapToDto(RTSPStream s) => new(
        s.Id, s.CameraName, s.CameraLocation, s.RtspUrl,
        s.FrameIntervalSeconds, s.IsActive, s.Status, s.LastChecked);
}
