using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PersonIdentificationSystem.API.Infrastructure;

public record FaceMatchResult(
    string PersonFaceId,
    decimal Confidence
);

public record SyncEmbeddingsResult(
    int Synced,
    int Failed,
    string Message
);

public interface IPythonFaceRecognitionClient
{
    Task<FaceMatchResult?> MatchFaceAsync(string imageBase64, CancellationToken ct = default);
    /// <summary>
    /// Register a face under the given CompreFace subject (PersonFaceId).
    /// </summary>
    Task<bool> RegisterFaceAsync(string personFaceId, string imageBase64, CancellationToken ct = default);
    Task UnregisterFaceAsync(string personFaceId, CancellationToken ct = default);
    Task WipeAllAsync(CancellationToken ct = default);
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}

public class PythonFaceRecognitionClient : IPythonFaceRecognitionClient
{
    private readonly HttpClient _http;
    private readonly ILogger<PythonFaceRecognitionClient> _logger;
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public PythonFaceRecognitionClient(HttpClient http, ILogger<PythonFaceRecognitionClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<FaceMatchResult?> MatchFaceAsync(string imageBase64, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new { image_base64 = imageBase64 });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        try
        {
            var response = await _http.PostAsync("/api/match", content, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Python service returned {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<PythonMatchResponse>(json, _json);

            if (result is null || !result.MatchFound || string.IsNullOrEmpty(result.PersonFaceId))
                return null;

            return new FaceMatchResult(result.PersonFaceId, result.Confidence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Python face recognition service");
            return null;
        }
    }

    public async Task<bool> RegisterFaceAsync(string personFaceId, string imageBase64, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            person_face_id = personFaceId,
            image_base64 = imageBase64
        });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        try
        {
            var response = await _http.PostAsync("/api/register", content, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("CompreFace register failed for face_id={FaceId}: {Status}", personFaceId, response.StatusCode);
                return false;
            }
            _logger.LogInformation("CompreFace register OK for face_id={FaceId}", personFaceId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CompreFace register error for face_id={FaceId}", personFaceId);
            return false;
        }
    }

    public async Task UnregisterFaceAsync(string personFaceId, CancellationToken ct = default)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { person_face_id = personFaceId });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            await _http.PostAsync("/api/unregister", content, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CompreFace unregister error for face_id={FaceId}", personFaceId);
        }
    }

    public async Task WipeAllAsync(CancellationToken ct = default)
    {
        try
        {
            var content = new StringContent("{}", Encoding.UTF8, "application/json");
            await _http.PostAsync("/api/wipe-all", content, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CompreFace wipe-all error");
        }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync("/health", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private class PythonMatchResponse
    {
        [JsonPropertyName("match_found")]
        public bool MatchFound { get; set; }

        [JsonPropertyName("person_face_id")]
        public string? PersonFaceId { get; set; }

        [JsonPropertyName("confidence")]
        public decimal Confidence { get; set; }
    }
}
