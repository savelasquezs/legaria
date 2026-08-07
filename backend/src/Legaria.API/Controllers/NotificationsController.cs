using System.ComponentModel.DataAnnotations;
using Legaria.API.Security;
using Legaria.Application.Authentication;
using Legaria.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Legaria.API.Controllers;

[ApiController]
[Route("api/tenant/whatsapp-channels")]
[Authorize(Policy = AuthorizationPolicies.TenantSuperAdministrator)]
public sealed class WhatsAppChannelsController(INotificationService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<WhatsAppChannelResult>>> List(CancellationToken ct) =>
        Ok(await service.ListChannelsAsync(currentUser.ToCurrentAccount(), ct));
    [HttpPost]
    public async Task<ActionResult<WhatsAppChannelResult>> Create(WhatsAppChannelInputModel input, CancellationToken ct)
    { var result = await service.CreateChannelAsync(input.ToInput(), currentUser.ToCurrentAccount(), ct); return Created(string.Empty, result); }
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WhatsAppChannelResult>> Update(Guid id, WhatsAppChannelInputModel input, CancellationToken ct) =>
        Ok(await service.UpdateChannelAsync(id, input.ToInput(), currentUser.ToCurrentAccount(), ct));
    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<WhatsAppChannelResult>> Activate(Guid id, CancellationToken ct) =>
        Ok(await service.SetChannelActiveAsync(id, true, currentUser.ToCurrentAccount(), ct));
    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<WhatsAppChannelResult>> Deactivate(Guid id, CancellationToken ct) =>
        Ok(await service.SetChannelActiveAsync(id, false, currentUser.ToCurrentAccount(), ct));
    [HttpPost("{id:guid}/test")]
    public async Task<ActionResult<WhatsAppConnectionResult>> Test(Guid id, CancellationToken ct) =>
        Ok(await service.TestChannelAsync(id, currentUser.ToCurrentAccount(), ct));
    [HttpPost("{id:guid}/sync-templates")]
    public async Task<ActionResult<TemplateSyncResult>> Sync(Guid id, CancellationToken ct) =>
        Ok(await service.SyncTemplatesAsync(id, currentUser.ToCurrentAccount(), ct));
}

[ApiController]
[Route("api/tenant/whatsapp-templates")]
[Authorize(Policy = AuthorizationPolicies.TenantSuperAdministrator)]
public sealed class WhatsAppTemplatesController(INotificationService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<WhatsAppTemplateResult>>> List([FromQuery] Guid? channelId,
        [FromQuery] string? status, [FromQuery] string? search, CancellationToken ct) =>
        Ok(await service.ListTemplatesAsync(channelId, status, search, currentUser.ToCurrentAccount(), ct));
}

[ApiController]
[Route("api/tenant/notification-rules")]
[Authorize(Policy = AuthorizationPolicies.TenantSuperAdministrator)]
public sealed class NotificationRulesController(INotificationService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<NotificationRuleResult>>> List(CancellationToken ct) =>
        Ok(await service.ListRulesAsync(currentUser.ToCurrentAccount(), ct));
    [HttpPost]
    public async Task<ActionResult<NotificationRuleResult>> Create(NotificationRuleInputModel input, CancellationToken ct)
    { var result = await service.CreateRuleAsync(input.ToInput(), currentUser.ToCurrentAccount(), ct); return Created(string.Empty, result); }
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<NotificationRuleResult>> Update(Guid id, NotificationRuleInputModel input, CancellationToken ct) =>
        Ok(await service.UpdateRuleAsync(id, input.ToInput(), currentUser.ToCurrentAccount(), ct));
    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<NotificationRuleResult>> Activate(Guid id, CancellationToken ct) =>
        Ok(await service.SetRuleActiveAsync(id, true, currentUser.ToCurrentAccount(), ct));
    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<NotificationRuleResult>> Deactivate(Guid id, CancellationToken ct) =>
        Ok(await service.SetRuleActiveAsync(id, false, currentUser.ToCurrentAccount(), ct));
}

[ApiController]
[Route("api/tenant")]
[Authorize(Policy = AuthorizationPolicies.TenantAdministrator)]
public sealed class NotificationManagementController(INotificationService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("notification-events")]
    [Authorize(Policy = AuthorizationPolicies.TenantSuperAdministrator)]
    public async Task<ActionResult<IReadOnlyCollection<NotificationEventResult>>> Events([FromQuery] int limit = 100,
        CancellationToken ct = default) => Ok(await service.ListEventsAsync(limit, currentUser.ToCurrentAccount(), ct));
    [HttpGet("notification-queue")]
    [Authorize(Policy = AuthorizationPolicies.TenantSuperAdministrator)]
    public async Task<ActionResult<IReadOnlyCollection<NotificationQueueResult>>> Queue([FromQuery] string? status,
        [FromQuery] int limit = 100, CancellationToken ct = default) => Ok(await service.ListQueueAsync(status, limit, currentUser.ToCurrentAccount(), ct));
    [HttpGet("me/notification-contact")]
    public async Task<ActionResult<NotificationContactResult>> Contact(CancellationToken ct) => Ok(await service.GetContactAsync(currentUser.ToCurrentAccount(), ct));
    [HttpPut("me/notification-contact")]
    public async Task<ActionResult<NotificationContactResult>> Contact(NotificationContactInputModel input, CancellationToken ct) =>
        Ok(await service.UpdateContactAsync(new(input.MobilePhone, input.WhatsAppConsent), currentUser.ToCurrentAccount(), ct));
    [HttpGet("notification-settings")]
    [Authorize(Policy = AuthorizationPolicies.TenantSuperAdministrator)]
    public async Task<ActionResult<NotificationSettingsResult>> Settings(CancellationToken ct) => Ok(await service.GetSettingsAsync(currentUser.ToCurrentAccount(), ct));
    [HttpPut("notification-settings")]
    [Authorize(Policy = AuthorizationPolicies.TenantSuperAdministrator)]
    public async Task<ActionResult<NotificationSettingsResult>> Settings(NotificationSettingsInputModel input, CancellationToken ct) =>
        Ok(await service.UpdateSettingsAsync(new(input.TimeZoneId, input.NotificationTime), currentUser.ToCurrentAccount(), ct));
}

[ApiController]
[Route("api/webhooks/whatsapp")]
[AllowAnonymous]
[EnableRateLimiting("whatsapp-webhook")]
public sealed class WhatsAppWebhookController(IWhatsAppWebhookService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Verify([FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? token, [FromQuery(Name = "hub.challenge")] string? challenge,
        CancellationToken ct)
    {
        if (mode != "subscribe" || token is null || challenge is null || !await service.VerifyAsync(token, ct)) return Forbid();
        return Content(challenge, "text/plain");
    }
    [HttpPost]
    [RequestSizeLimit(1_048_576)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        await using var memory = new MemoryStream();
        await Request.Body.CopyToAsync(memory, ct);
        return await service.ProcessAsync(memory.ToArray(), Request.Headers["X-Hub-Signature-256"].FirstOrDefault(), ct) ? Ok() : Unauthorized();
    }
}

public sealed record WhatsAppChannelInputModel([Required, MaxLength(150)] string Name,
    [Required, MaxLength(64)] string PhoneNumberId, [Required, MaxLength(64)] string BusinessAccountId,
    string? AccessToken, string? WebhookVerifyToken, string? AppSecret)
{ public WhatsAppChannelInput ToInput() => new(Name, PhoneNumberId, BusinessAccountId, AccessToken, WebhookVerifyToken, AppSecret); }
public sealed record NotificationRuleInputModel([Required, MaxLength(150)] string Name, [Required] Guid DocumentTypeId,
    [Required] Guid WhatsAppChannelId, [Required] Guid WhatsAppTemplateId, [Required] string Priority,
    [Required, MinLength(1)] IReadOnlyCollection<string> Recipients,
    [Required] IReadOnlyDictionary<string, string> VariableMappings,
    [Required, MinLength(1), MaxLength(3)] IReadOnlyCollection<NotificationScheduleInput> Schedules)
{ public NotificationRuleInput ToInput() => new(Name, DocumentTypeId, WhatsAppChannelId, WhatsAppTemplateId, Priority, Recipients, VariableMappings, Schedules); }
public sealed record NotificationContactInputModel([MaxLength(16)] string? MobilePhone, bool WhatsAppConsent);
public sealed record NotificationSettingsInputModel([Required, MaxLength(100)] string TimeZoneId, TimeOnly NotificationTime);
