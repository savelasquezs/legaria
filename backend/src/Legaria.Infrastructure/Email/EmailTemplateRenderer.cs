using System.Net;
using System.Reflection;
using Legaria.Application.Authentication;
using Legaria.Application.Configuration;

namespace Legaria.Infrastructure.Email;

public sealed class EmailTemplateRenderer(ResendOptions options) : IEmailTemplateRenderer
{
    private const string ResourcePrefix = "Legaria.Infrastructure.EmailTemplates";

    public string RenderVerification(string firstName, string verificationUrl, TimeSpan expiration) =>
        Render(
            "VerifyEmail.html",
            firstName,
            verificationUrl,
            $"{(int)expiration.TotalHours} horas");

    public string RenderPasswordReset(string firstName, string resetUrl, TimeSpan expiration) =>
        Render(
            "ResetPassword.html",
            firstName,
            resetUrl,
            $"{(int)expiration.TotalMinutes} minutos");

    public string RenderTenantInvitation(
        string firstName,
        string organizationName,
        string invitationUrl,
        TimeSpan expiration) =>
        Render(
                "TenantInvitation.html",
                firstName,
                invitationUrl,
                $"{(int)expiration.TotalHours} horas")
            .Replace("{{OrganizationName}}", WebUtility.HtmlEncode(organizationName), StringComparison.Ordinal);

    private string Render(string resourceName, string firstName, string actionUrl, string expiration)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream($"{ResourcePrefix}.{resourceName}")
            ?? throw new InvalidOperationException($"No se encontró la plantilla {resourceName}.");
        using var reader = new StreamReader(stream);
        var template = reader.ReadToEnd();
        var supportEmail = string.IsNullOrWhiteSpace(options.ReplyToEmail)
            ? options.FromEmail
            : options.ReplyToEmail;

        return template
            .Replace("{{FirstName}}", WebUtility.HtmlEncode(firstName), StringComparison.Ordinal)
            .Replace("{{ActionUrl}}", WebUtility.HtmlEncode(actionUrl), StringComparison.Ordinal)
            .Replace("{{ExpirationTime}}", WebUtility.HtmlEncode(expiration), StringComparison.Ordinal)
            .Replace("{{SupportEmail}}", WebUtility.HtmlEncode(supportEmail), StringComparison.Ordinal);
    }
}
