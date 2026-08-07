using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Legaria.Application.Configuration;
using Legaria.Application.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Legaria.Infrastructure.Notifications;

public sealed partial class WhatsAppCloudClient(HttpClient httpClient, IOptions<WhatsAppCloudOptions> options,
    ILogger<WhatsAppCloudClient> logger) : IWhatsAppCloudClient
{
    private readonly WhatsAppCloudOptions _options = options.Value;

    public async Task<MetaConnectionResult> TestConnectionAsync(string phoneNumberId, string businessAccountId,
        string accessToken, CancellationToken cancellationToken)
    {
        var phone = await GetAsync($"{phoneNumberId}?fields=id,display_phone_number,verified_name", accessToken, cancellationToken);
        if (!phone.Success) return new(false, null, phone.Error);
        var business = await GetAsync($"{businessAccountId}?fields=id,name", accessToken, cancellationToken);
        if (!business.Success) return new(false, null, business.Error);
        using var document = JsonDocument.Parse(phone.Body!);
        return new(true, GetString(document.RootElement, "display_phone_number"), null);
    }

    public async Task<MetaTemplateSyncResult> GetTemplatesAsync(string businessAccountId, string accessToken,
        CancellationToken cancellationToken)
    {
        var templates = new List<MetaTemplate>();
        string? url = BuildUrl($"{businessAccountId}/message_templates?fields=id,name,language,category,status,components&limit=100");
        while (url is not null)
        {
            var page = await GetAbsoluteAsync(url, accessToken, cancellationToken);
            if (!page.Success) return new(false, [], page.Error);
            using var document = JsonDocument.Parse(page.Body!);
            if (document.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in data.EnumerateArray())
                {
                    var id = GetString(item, "id"); var name = GetString(item, "name"); var language = GetString(item, "language");
                    if (id is null || name is null || language is null) continue;
                    templates.Add(new(id, name, language, GetString(item, "category") ?? string.Empty,
                        GetString(item, "status") ?? string.Empty,
                        item.TryGetProperty("components", out var components) ? components.GetRawText() : "[]"));
                }
            }
            url = document.RootElement.TryGetProperty("paging", out var paging) && paging.TryGetProperty("next", out var next)
                ? next.GetString() : null;
            if (url is not null && !url.StartsWith(_options.BaseUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                return new(false, [], "Meta devolvió una URL de paginación inválida.");
        }
        return new(true, templates, null);
    }

    public async Task<MetaTemplateSendResult> SendTemplateAsync(string phoneNumberId, string accessToken,
        string destination, string templateName, string language, string componentsJson,
        IReadOnlyDictionary<string, string> mappings, IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = destination.TrimStart('+'),
            type = "template",
            template = new { name = templateName, language = new { code = language }, components = BuildComponents(componentsJson, mappings, values) }
        };
        var requestJson = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUrl($"{phoneNumberId}/messages"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(payload);
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var safe = Sanitize(body, accessToken);
            if (!response.IsSuccessStatusCode)
            {
                var transient = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
                var retry = response.Headers.RetryAfter?.Delta;
                if (retry is null && response.Headers.RetryAfter?.Date is { } retryAt)
                {
                    var delay = retryAt - DateTimeOffset.UtcNow;
                    retry = delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
                }
                return new(false, transient, null, requestJson, LimitJson(safe), $"HTTP_{(int)response.StatusCode}",
                    Sanitize(MetaError(body) ?? $"Meta respondió {(int)response.StatusCode}.", accessToken), retry);
            }
            using var document = JsonDocument.Parse(body);
            var messageId = document.RootElement.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array && messages.GetArrayLength() > 0
                ? GetString(messages[0], "id") : null;
            return new(true, false, messageId, requestJson, LimitJson(safe), null, null, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning("WhatsApp send failed: {Error}", Sanitize(ex.Message, accessToken));
            return new(false, true, null, requestJson, null, "NETWORK_ERROR", "No fue posible comunicarse con Meta.", null);
        }
    }

    private async Task<HttpResult> GetAsync(string relative, string token, CancellationToken ct) =>
        await GetAbsoluteAsync(BuildUrl(relative), token, ct);
    private async Task<HttpResult> GetAbsoluteAsync(string url, string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            using var response = await httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            return response.IsSuccessStatusCode ? new(true, body, null) : new(false, null,
                Sanitize(MetaError(body) ?? $"Meta respondió {(int)response.StatusCode}.", token));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        { logger.LogWarning("WhatsApp request failed: {Error}", Sanitize(ex.Message, token)); return new(false, null, "No fue posible comunicarse con Meta."); }
    }

    private object[] BuildComponents(string componentsJson, IReadOnlyDictionary<string, string> mappings,
        IReadOnlyDictionary<string, string> values)
    {
        using var document = JsonDocument.Parse(componentsJson);
        if (document.RootElement.ValueKind != JsonValueKind.Array) return [];
        var output = new List<object>();
        var componentIndex = 0;
        foreach (var component in document.RootElement.EnumerateArray())
        {
            var type = GetString(component, "type")?.ToUpperInvariant();
            if (type is "HEADER" or "BODY")
            {
                var text = GetString(component, "text") ?? string.Empty;
                var parameters = ParametersFor(text, $"$[{componentIndex}].text", mappings, values);
                if (parameters.Length > 0) output.Add(new { type = type.ToLowerInvariant(), parameters });
            }
            if (type == "BUTTONS" && component.TryGetProperty("buttons", out var buttons) && buttons.ValueKind == JsonValueKind.Array)
            {
                var buttonIndex = 0;
                foreach (var button in buttons.EnumerateArray())
                {
                    var url = GetString(button, "url") ?? string.Empty;
                    var parameters = ParametersFor(url, $"$[{componentIndex}].buttons[{buttonIndex}].url", mappings, values);
                    if (parameters.Length > 0) output.Add(new { type = "button", sub_type = "url", index = buttonIndex.ToString(), parameters });
                    buttonIndex++;
                }
            }
            componentIndex++;
        }
        return output.ToArray();
    }

    private static object[] ParametersFor(string text, string path, IReadOnlyDictionary<string, string> mappings, IReadOnlyDictionary<string, string> values) =>
        PlaceholderRegex().Matches(text).Select(match =>
        {
            var key = $"{path}:{match.Groups[1].Value}";
            return new { type = "text", text = mappings.TryGetValue(key, out var variable) && values.TryGetValue(variable, out var value) ? value : string.Empty };
        }).Cast<object>().ToArray();
    private string BuildUrl(string relative) => $"{_options.BaseUrl.TrimEnd('/')}/{_options.GraphApiVersion.Trim('/')}/{relative.TrimStart('/')}";
    private static string? GetString(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static string? MetaError(string body) { try { using var doc = JsonDocument.Parse(body); return doc.RootElement.TryGetProperty("error", out var error) ? GetString(error, "message") : null; } catch (JsonException) { return null; } }
    private static string Sanitize(string value, string token) => TokenRegex().Replace(value.Replace(token, "[REDACTED]", StringComparison.Ordinal), "$1[REDACTED]");
    private static string LimitJson(string value) { var clean = value.Length > 32768 ? value[..32768] : value; try { JsonDocument.Parse(clean); return clean; } catch (JsonException) { return JsonSerializer.Serialize(new { response = clean }); } }
    private sealed record HttpResult(bool Success, string? Body, string? Error);
    [GeneratedRegex(@"\{\{\s*([^{}]+?)\s*\}\}")] private static partial Regex PlaceholderRegex();
    [GeneratedRegex("(?i)(\\\"(?:access_token|authorization)\\\"\\s*:\\s*\\\")[^\\\"]+")] private static partial Regex TokenRegex();
}
