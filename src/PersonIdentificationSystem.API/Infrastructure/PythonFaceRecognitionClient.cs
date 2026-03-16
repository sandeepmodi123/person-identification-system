using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PersonIdentificationSystem.API.Infrastructure;

public record FaceMatchResult(
    Guid PersonId,
    decimal Confidence,
    string? PersonName
);

public interface IPythonFaceRecognitionClient
{
    Task<FaceMatchResult?> MatchFaceAsync(string imageBase64, CancellationToken ct = default);
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

            if (result is null || !result.MatchFound || result.PersonId is null)
                return null;

            return new FaceMatchResult(result.PersonId.Value, result.Confidence, result.PersonName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Python face recognition service");
            return null;
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

        [JsonPropertyName("person_id")]
        public Guid? PersonId { get; set; }

        [JsonPropertyName("person_name")]
        public string? PersonName { get; set; }

        [JsonPropertyName("confidence")]
        public decimal Confidence { get; set; }
    }
}
