using System.ComponentModel.DataAnnotations;
using Legaria.Application.Authentication;
using Legaria.Application.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Legaria.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAuthenticationService authenticationService,
    ICurrentUser currentUser,
    FrontendOptions frontendOptions) : ControllerBase
{
    public const string RefreshCookieName = "legaria_refresh";

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<AuthenticationResponse>> Login(
        LoginInput input,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.LoginAsync(
            new LoginRequest(input.Email, input.Password),
            CreateClientContext(),
            cancellationToken);
        SetRefreshCookie(result);
        return Ok(AuthenticationResponse.From(result));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("refresh")]
    public async Task<ActionResult<AuthenticationResponse>> Refresh(CancellationToken cancellationToken)
    {
        EnsureTrustedOrigin();
        var rawToken = Request.Cookies[RefreshCookieName] ?? string.Empty;
        var result = await authenticationService.RefreshAsync(
            rawToken,
            CreateClientContext(),
            cancellationToken);
        SetRefreshCookie(result);
        return Ok(AuthenticationResponse.From(result));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        EnsureTrustedOrigin();
        await authenticationService.LogoutAsync(
            Request.Cookies[RefreshCookieName],
            CreateClientContext(),
            cancellationToken);
        DeleteRefreshCookie();
        return NoContent();
    }

    [HttpPost("logout-all")]
    [Authorize]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        EnsureTrustedOrigin();
        await authenticationService.LogoutAllAsync(
            currentUser.ToCurrentAccount(),
            CreateClientContext(),
            cancellationToken);
        DeleteRefreshCookie();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<AuthenticatedAccount>> Me(CancellationToken cancellationToken) =>
        Ok(await authenticationService.GetCurrentAsync(
            currentUser.ToCurrentAccount(),
            cancellationToken));

    [HttpPost("verify-email")]
    [AllowAnonymous]
    [EnableRateLimiting("account-token")]
    public async Task<IActionResult> VerifyEmail(
        TokenInput input,
        CancellationToken cancellationToken)
    {
        await authenticationService.VerifyEmailAsync(
            input.Token,
            CreateClientContext(),
            cancellationToken);
        return NoContent();
    }

    [HttpPost("resend-verification")]
    [AllowAnonymous]
    [EnableRateLimiting("email-request")]
    public async Task<ActionResult<GenericMessageResponse>> ResendVerification(
        EmailInput input,
        CancellationToken cancellationToken)
    {
        await authenticationService.RequestEmailVerificationAsync(
            input.Email,
            CreateClientContext(),
            cancellationToken);
        return Accepted(new GenericMessageResponse(AuthenticationService.GetGenericRecoveryMessage()));
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("email-request")]
    public async Task<ActionResult<GenericMessageResponse>> ForgotPassword(
        EmailInput input,
        CancellationToken cancellationToken)
    {
        await authenticationService.RequestPasswordResetAsync(
            input.Email,
            CreateClientContext(),
            cancellationToken);
        return Accepted(new GenericMessageResponse(AuthenticationService.GetGenericRecoveryMessage()));
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("account-token")]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordInput input,
        CancellationToken cancellationToken)
    {
        await authenticationService.ResetPasswordAsync(
            new ResetPasswordRequest(input.Token, input.NewPassword),
            CreateClientContext(),
            cancellationToken);
        return NoContent();
    }

    private ClientContext CreateClientContext() =>
        new(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString() is { Length: > 512 } userAgent
                ? userAgent[..512]
                : Request.Headers.UserAgent.ToString());

    private void SetRefreshCookie(AuthenticationResult result)
    {
        Response.Cookies.Append(
            RefreshCookieName,
            result.RefreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/api/auth",
                Expires = result.RefreshTokenExpiresAt,
                IsEssential = true
            });
    }

    private void DeleteRefreshCookie()
    {
        Response.Cookies.Delete(
            RefreshCookieName,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/api/auth",
                IsEssential = true
            });
    }

    private void EnsureTrustedOrigin()
    {
        var origin = Request.Headers.Origin.ToString();
        if (string.IsNullOrWhiteSpace(origin))
        {
            return;
        }

        var expectedOrigin = new Uri(frontendOptions.BaseUrl).GetLeftPart(UriPartial.Authority);
        if (!string.Equals(origin.TrimEnd('/'), expectedOrigin.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
        {
            throw new AuthException(AuthErrorCodes.UntrustedOrigin, "El origen de la solicitud no está autorizado.");
        }
    }
}

public sealed record LoginInput(
    [Required, EmailAddress, MaxLength(320)] string Email,
    [Required, MaxLength(128)] string Password);

public sealed record EmailInput(
    [Required, EmailAddress, MaxLength(320)] string Email);

public sealed record TokenInput(
    [Required, MaxLength(256)] string Token);

public sealed record ResetPasswordInput(
    [Required, MaxLength(256)] string Token,
    [Required, MinLength(8), MaxLength(128)] string NewPassword);

public sealed record GenericMessageResponse(string Message);

public sealed record AuthenticationResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    AuthenticatedAccount Account)
{
    public static AuthenticationResponse From(AuthenticationResult result) =>
        new(result.AccessToken, result.ExpiresAt, result.Account);
}
