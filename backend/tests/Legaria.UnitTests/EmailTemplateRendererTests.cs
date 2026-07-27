using Legaria.Application.Configuration;
using Legaria.Infrastructure.Email;

namespace Legaria.UnitTests;

public sealed class EmailTemplateRendererTests
{
    [Fact]
    public void VerificationTemplate_EncodesUserControlledValues()
    {
        var renderer = new EmailTemplateRenderer(
            new ResendOptions
            {
                FromEmail = "soporte@legaria.test",
                FromName = "Legaria",
                ApiKey = "test"
            });

        var html = renderer.RenderVerification(
            "<script>alert(1)</script>",
            "https://legaria.test/verify-email?token=abc&other=1",
            TimeSpan.FromHours(24));

        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("token=abc&amp;other=1", html);
        Assert.Contains("24 horas", html);
    }
}
