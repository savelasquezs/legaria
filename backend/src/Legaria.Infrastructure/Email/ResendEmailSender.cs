using Legaria.Application.Authentication;
using Legaria.Application.Configuration;
using Microsoft.Extensions.Logging;
using Resend;
using ApplicationEmailMessage = Legaria.Application.Authentication.EmailMessage;
using ResendEmailMessage = Resend.EmailMessage;

namespace Legaria.Infrastructure.Email;

public sealed class ResendEmailSender(
    IResend resend,
    ResendOptions options,
    ILogger<ResendEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(
        ApplicationEmailMessage message,
        CancellationToken cancellationToken = default)
    {
        var resendMessage = new ResendEmailMessage
        {
            From = $"{options.FromName} <{options.FromEmail}>",
            Subject = message.Subject,
            HtmlBody = message.HtmlBody,
            TextBody = message.TextBody
        };
        resendMessage.To.Add(message.Recipient);
        if (!string.IsNullOrWhiteSpace(options.ReplyToEmail))
        {
            resendMessage.ReplyTo = [options.ReplyToEmail];
        }

        try
        {
            await resend.EmailSendAsync(resendMessage, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError("Resend superó el tiempo máximo de envío.");
            throw new EmailDeliveryException("El proveedor de correo no respondió a tiempo.");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Resend rechazó o no pudo completar un correo transaccional.");
            throw new EmailDeliveryException("No fue posible enviar el correo.", exception);
        }
    }
}
