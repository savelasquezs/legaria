using Legaria.Application.Notifications;
using Legaria.Domain.Notifications;
using Legaria.Infrastructure.Notifications;
using Microsoft.Extensions.Configuration;

namespace Legaria.UnitTests;

public sealed class WhatsAppNotificationTests
{
    [Fact]
    public void TemplateParserDetectsBodyHeaderAndButtonVariables()
    {
        const string components = """
        [
          {"type":"HEADER","format":"TEXT","text":"Vence {{1}}"},
          {"type":"BODY","text":"Hola {{employee}}, tu documento {{2}} vence pronto."},
          {"type":"BUTTONS","buttons":[{"type":"URL","text":"Ver","url":"https://legaria.test/{{1}}"}]}
        ]
        """;

        var variables = WhatsAppTemplateParser.DetectVariables(components);

        Assert.Equal(4, variables.Count);
        Assert.Contains("$[0].text:1", variables);
        Assert.Contains("$[1].text:employee", variables);
        Assert.Contains("$[1].text:2", variables);
        Assert.Contains("$[2].buttons[0].url:1", variables);
        Assert.Contains("\"type\":\"URL\"", WhatsAppTemplateParser.DetectButtonsJson(components));
    }

    [Fact]
    public void SecretProtectorRoundTripsWithoutExposingPlainText()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Integrations:EncryptionKey"] = "unit-test-key-that-is-long-and-stable" }).Build();
        var protector = new IntegrationSecretProtector(configuration);

        var encrypted = protector.Protect("EA-secret-token");

        Assert.StartsWith("v1.", encrypted);
        Assert.DoesNotContain("EA-secret-token", encrypted);
        Assert.Equal("EA-secret-token", protector.Unprotect(encrypted));
    }

    [Theory]
    [InlineData("DAY", 15, "2026-03-16")]
    [InlineData("WEEK", 2, "2026-03-17")]
    [InlineData("MONTH", 1, "2026-02-28")]
    public void SchedulesUseDaysWeeksAndCalendarMonths(string unit, int amount, string expected)
    {
        var schedule = NotificationRuleSchedule.Create(Guid.NewGuid(), Guid.NewGuid(), amount, unit);

        Assert.Equal(DateOnly.Parse(expected), schedule.ScheduledOn(new DateOnly(2026, 3, 31)));
    }

    [Fact]
    public void ReplacedSchedulesRemainAsInactiveHistory()
    {
        var schedule = NotificationRuleSchedule.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "MONTH");

        schedule.Deactivate();

        Assert.False(schedule.IsActive);
        Assert.Equal(new DateOnly(2026, 2, 28), schedule.ScheduledOn(new DateOnly(2026, 3, 31)));
    }

    [Fact]
    public void QueueKeepsProviderAcceptanceSeparateFromDeliveryStatus()
    {
        var now = new DateTimeOffset(2026, 8, 5, 13, 0, 0, TimeSpan.Zero);
        var item = NotificationQueueItem.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "EMPLOYEE", Guid.NewGuid(), "+573001234567", "dedupe", "HIGH", "{}", now);

        item.Claim("worker", now);
        item.RecordAttempt();
        item.Sent("wamid.1", now.AddSeconds(1));
        item.UpdateDelivery("DELIVERED", null, now.AddSeconds(2));

        Assert.Equal(NotificationCodes.Sent, item.Status);
        Assert.Equal("DELIVERED", item.DeliveryStatus);
        Assert.Equal(1, item.AttemptCount);
    }
}
