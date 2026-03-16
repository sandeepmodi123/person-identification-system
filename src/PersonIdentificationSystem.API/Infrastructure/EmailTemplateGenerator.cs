namespace PersonIdentificationSystem.API.Infrastructure;

public static class EmailTemplateGenerator
{
    public static string GenerateDetectionAlert(
        string personName,
        string riskLevel,
        string? description,
        DateTime detectedAt,
        double confidenceScore,
        string detectionId)
    {
        var riskColor = riskLevel switch
        {
            "Critical" => "#d32f2f",
            "High" => "#f57c00",
            "Medium" => "#fbc02d",
            _ => "#388e3c"
        };

        return $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family:Arial,sans-serif;background:#f5f5f5;padding:20px;">
              <div style="max-width:600px;margin:0 auto;background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.1);">
                <div style="background:{riskColor};color:#fff;padding:20px;">
                  <h1 style="margin:0;font-size:24px;">🚨 Person Detection Alert</h1>
                  <p style="margin:5px 0 0;">Risk Level: <strong>{riskLevel}</strong></p>
                </div>
                <div style="padding:24px;">
                  <h2 style="color:#333;">{personName}</h2>
                  {(description is not null ? $"<p style=\"color:#666;\">{description}</p>" : "")}
                  <table style="width:100%;border-collapse:collapse;margin-top:16px;">
                    <tr>
                      <td style="padding:8px;border-bottom:1px solid #eee;color:#888;width:40%;">Detected At</td>
                      <td style="padding:8px;border-bottom:1px solid #eee;">{detectedAt:yyyy-MM-dd HH:mm:ss} UTC</td>
                    </tr>
                    <tr>
                      <td style="padding:8px;border-bottom:1px solid #eee;color:#888;">Confidence Score</td>
                      <td style="padding:8px;border-bottom:1px solid #eee;">{confidenceScore:P1}</td>
                    </tr>
                    <tr>
                      <td style="padding:8px;color:#888;">Detection ID</td>
                      <td style="padding:8px;font-family:monospace;font-size:12px;">{detectionId}</td>
                    </tr>
                  </table>
                  <div style="margin-top:24px;text-align:center;">
                    <a href="/detections/{detectionId}" 
                       style="background:{riskColor};color:#fff;padding:12px 24px;text-decoration:none;border-radius:4px;display:inline-block;">
                      View Detection Details
                    </a>
                  </div>
                </div>
                <div style="background:#f5f5f5;padding:12px;text-align:center;font-size:12px;color:#999;">
                  Person Identification System &mdash; Automated Alert
                </div>
              </div>
            </body>
            </html>
            """;
    }
}
