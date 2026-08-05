using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Legaria.API.Hosting;

public static class GoogleCredentialBootstrap
{
    public static void Apply(IConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS"))) return;
        var json = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS_JSON");
        if (string.IsNullOrWhiteSpace(json))
        {
            var encoded = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS_JSON_BASE64");
            if (!string.IsNullOrWhiteSpace(encoded))
            {
                try { json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded.Trim())); }
                catch (FormatException) { json = null; }
            }
        }
        if (!string.IsNullOrWhiteSpace(json) && json.TrimStart().StartsWith('{'))
        {
            using var _ = JsonDocument.Parse(json);
            var stamp = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)))[..16];
            var temporaryPath = Path.Combine(Path.GetTempPath(), $"legaria-gcp-{stamp}.json");
            if (!File.Exists(temporaryPath)) File.WriteAllText(temporaryPath, json, Encoding.UTF8);
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", temporaryPath);
            return;
        }
        var configuredPath = configuration["FirebaseStorage:GoogleApplicationCredentialsPath"];
        if (string.IsNullOrWhiteSpace(configuredPath)) return;
        var fullPath = Path.IsPathRooted(configuredPath) ? configuredPath : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), configuredPath));
        if (File.Exists(fullPath)) Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", fullPath);
    }
}
