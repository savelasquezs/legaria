using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Legaria.Application.Authentication;
using Legaria.Application.Notifications;
using Legaria.Domain.Notifications;
using Legaria.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Legaria.Infrastructure.Notifications;

public sealed class WhatsAppWebhookService(
    LegariaDbContext db,
    IIntegrationSecretProtector protector,
    ISecureTokenService tokens,
    IClock clock) : IWhatsAppWebhookService
{
    public Task<bool> VerifyAsync(string verifyToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(verifyToken)) return Task.FromResult(false);
        var hash = tokens.HashToken(verifyToken);
        return db.WhatsAppChannels.AsNoTracking().AnyAsync(x => x.WebhookVerifyTokenHash == hash && x.Status == NotificationCodes.Active, ct);
    }

    public async Task<bool> ProcessAsync(byte[] body, string? signature, CancellationToken ct)
    {
        if (body.Length == 0 || body.Length > 1_048_576 || string.IsNullOrWhiteSpace(signature)) return false;
        JsonDocument document;
        try { document = JsonDocument.Parse(body); }
        catch (JsonException) { return false; }
        using (document)
        {
            var values = Values(document.RootElement).ToArray();
            var phoneNumberId = values.Select(GetPhoneNumberId).FirstOrDefault(x => x is not null);
            if (phoneNumberId is null) return false;
            var channel = await db.WhatsAppChannels.FirstOrDefaultAsync(x => x.PhoneNumberId == phoneNumberId && x.Status == NotificationCodes.Active, ct);
            if (channel is null || !ValidSignature(body, signature, protector.Unprotect(channel.EncryptedAppSecret))) return false;

            foreach (var value in values)
            {
                if (!value.TryGetProperty("statuses", out var statuses) || statuses.ValueKind != JsonValueKind.Array) continue;
                foreach (var status in statuses.EnumerateArray())
                {
                    var messageId = GetString(status, "id");
                    var providerStatus = GetString(status, "status")?.ToUpperInvariant();
                    var timestamp = GetString(status, "timestamp") ?? string.Empty;
                    if (messageId is null || providerStatus is not ("SENT" or "DELIVERED" or "READ" or "FAILED")) continue;
                    var eventKey = $"{messageId}:{providerStatus}:{timestamp}";
                    if (await db.WhatsAppWebhookReceipts.AnyAsync(x => x.EventKey == eventKey, ct)) continue;
                    var queue = await db.NotificationQueueItems.FirstOrDefaultAsync(x => x.ProviderMessageId == messageId, ct);
                    if (queue is not null) queue.UpdateDelivery(providerStatus, providerStatus == "FAILED" ? ErrorMessage(status) : null, clock.UtcNow);
                    db.Add(WhatsAppWebhookReceipt.Create(channel.OrganizationId, channel.Id, eventKey, clock.UtcNow));
                }
            }
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                db.ChangeTracker.Clear();
            }
            return true;
        }
    }

    private static IEnumerable<JsonElement> Values(JsonElement root)
    {
        if (!root.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array) yield break;
        foreach (var entry in entries.EnumerateArray())
            if (entry.TryGetProperty("changes", out var changes) && changes.ValueKind == JsonValueKind.Array)
                foreach (var change in changes.EnumerateArray())
                    if (change.TryGetProperty("value", out var value)) yield return value;
    }
    private static string? GetPhoneNumberId(JsonElement value) => value.TryGetProperty("metadata", out var metadata) ? GetString(metadata, "phone_number_id") : null;
    private static string? GetString(JsonElement value, string name) => value.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.String ? item.GetString() : null;
    private static string? ErrorMessage(JsonElement status)
    {
        if (!status.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Array || errors.GetArrayLength() == 0) return "Meta informó que el mensaje falló.";
        var message = GetString(errors[0], "title") ?? GetString(errors[0], "message") ?? "Meta informó que el mensaje falló.";
        return message.Length > 1000 ? message[..1000] : message;
    }
    private static bool ValidSignature(byte[] body, string signature, string secret)
    {
        if (!signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)) return false;
        byte[] received;
        try { received = Convert.FromHexString(signature[7..]); } catch (FormatException) { return false; }
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body);
        return received.Length == expected.Length && CryptographicOperations.FixedTimeEquals(received, expected);
    }
}
