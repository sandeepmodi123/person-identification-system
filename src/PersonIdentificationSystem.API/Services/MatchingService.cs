using Microsoft.AspNetCore.SignalR;
using PersonIdentificationSystem.API.DTOs;
using PersonIdentificationSystem.API.Hubs;
using PersonIdentificationSystem.API.Infrastructure;
using PersonIdentificationSystem.API.Models.Entities;
using PersonIdentificationSystem.API.Repositories;

namespace PersonIdentificationSystem.API.Services;

public interface IMatchingService
{
    Task<ProcessFrameResult> ProcessFrameAsync(ProcessFrameRequest request, CancellationToken ct = default);
}

public class MatchingService : IMatchingService
{
    private readonly IPythonFaceRecognitionClient _pythonClient;
    private readonly IPersonRepository _personRepo;
    private readonly IDetectionService _detectionService;
    private readonly INotificationService _notificationService;
    private readonly IStreamRepository _streamRepo;
    private readonly IHubContext<DetectionHub> _hubContext;
    private readonly IConfiguration _config;
    private readonly ILogger<MatchingService> _logger;

    public MatchingService(
        IPythonFaceRecognitionClient pythonClient,
        IPersonRepository personRepo,
        IDetectionService detectionService,
        INotificationService notificationService,
        IStreamRepository streamRepo,
        IHubContext<DetectionHub> hubContext,
        IConfiguration config,
        ILogger<MatchingService> logger)
    {
        _pythonClient = pythonClient;
        _personRepo = personRepo;
        _detectionService = detectionService;
        _notificationService = notificationService;
        _streamRepo = streamRepo;
        _hubContext = hubContext;
        _config = config;
        _logger = logger;
    }

    public async Task<ProcessFrameResult> ProcessFrameAsync(ProcessFrameRequest request, CancellationToken ct = default)
    {
        var threshold = _config.GetValue<decimal>("Matching:ConfidenceThreshold", 0.85m);

        // 1. Call Python service for face matching
        var matchResult = await _pythonClient.MatchFaceAsync(request.FrameBase64, ct);

        if (matchResult is null || matchResult.Confidence < threshold)
        {
            return new ProcessFrameResult(false, null, null, null, null, false);
        }

        // 2. Look up person
        var person = await _personRepo.GetByIdAsync(matchResult.PersonId, ct);
        if (person is null || !person.IsActive)
        {
            return new ProcessFrameResult(false, null, null, null, null, false);
        }

        // 3. Persist detection
        var detection = await _detectionService.CreateDetectionAsync(
            request.StreamId, person.Id, matchResult.Confidence, null,
            System.Text.Json.JsonSerializer.Serialize(matchResult), ct);

        // 4. Send notification
        bool notified = false;
        try
        {
            notified = await _notificationService.SendDetectionNotificationAsync(detection, person, ct);
            if (notified)
            {
                detection.EmailSent = true;
                // EmailSent flag update is handled in notification service
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification for detection {DetectionId}", detection.Id);
        }

        // 5. Broadcast real-time event via SignalR
        await BroadcastDetectionAsync(detection, person, request.StreamId, matchResult.Confidence, notified, ct);

        return new ProcessFrameResult(true, detection.Id, person.Id, person.Name, matchResult.Confidence, notified);
    }

    private async Task BroadcastDetectionAsync(Detection detection, Person person, Guid streamId, decimal confidence, bool notified, CancellationToken ct)
    {
        try
        {
            var stream = await _streamRepo.GetByIdAsync(streamId, ct);
            var detectionEvent = new DetectionEventDto(
                detection.Id,
                streamId,
                stream?.CameraName ?? "Unknown",
                person.Id,
                person.Name,
                person.RiskLevel,
                confidence,
                detection.DetectionTimestamp,
                notified
            );
            await _hubContext.Clients.All.SendAsync("DetectionReceived", detectionEvent, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast SignalR detection event");
        }
    }
}
