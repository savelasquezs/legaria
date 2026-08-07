namespace Legaria.Application.Configuration;

public sealed class WhatsAppCloudOptions
{
    public const string SectionName = "WhatsAppCloud";
    public string BaseUrl { get; init; } = "https://graph.facebook.com";
    public string GraphApiVersion { get; init; } = "v23.0";
    public int TimeoutSeconds { get; init; } = 15;
}
