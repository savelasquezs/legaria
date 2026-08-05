using System.IdentityModel.Tokens.Jwt;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Legaria.API.Middleware;
using Legaria.API.Security;
using Legaria.Application.Authentication;
using Legaria.Application.Branches;
using Legaria.Application.Configuration;
using Legaria.Application.Organizations;
using Legaria.Domain.Authentication;
using Legaria.Infrastructure.Authentication;
using Legaria.Infrastructure.Email;
using Legaria.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Resend;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Falta ConnectionStrings__DefaultConnection.");
}

try
{
    _ = new NpgsqlConnectionStringBuilder(connectionString);
}
catch (ArgumentException exception)
{
    throw new InvalidOperationException(
        "ConnectionStrings__DefaultConnection no es una conexión PostgreSQL válida.",
        exception);
}

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.Issuer) ||
    string.IsNullOrWhiteSpace(jwtOptions.Audience) ||
    Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < 32 ||
    jwtOptions.AccessTokenMinutes != 10)
{
    throw new InvalidOperationException(
        "Jwt__Issuer, Jwt__Audience y una Jwt__SigningKey de al menos 32 bytes son obligatorios; " +
        "Jwt__AccessTokenMinutes debe ser 10.");
}

var resendOptions = builder.Configuration.GetSection(ResendOptions.SectionName).Get<ResendOptions>()
    ?? new ResendOptions();
if (string.IsNullOrWhiteSpace(resendOptions.ApiKey) ||
    !IsValidEmail(resendOptions.FromEmail) ||
    string.IsNullOrWhiteSpace(resendOptions.FromName) ||
    !IsValidEmail(resendOptions.ReplyToEmail))
{
    throw new InvalidOperationException(
        "Resend__ApiKey, Resend__FromEmail, Resend__FromName y un " +
        "Resend__ReplyToEmail válido son obligatorios.");
}

var frontendOptions = builder.Configuration.GetSection(FrontendOptions.SectionName).Get<FrontendOptions>()
    ?? new FrontendOptions();
if (!Uri.TryCreate(frontendOptions.BaseUrl, UriKind.Absolute, out var frontendUri) ||
    frontendUri.Scheme is not ("http" or "https"))
{
    throw new InvalidOperationException("Frontend__BaseUrl debe ser una URL absoluta HTTP o HTTPS.");
}

var bootstrapOptions = builder.Configuration
    .GetSection(BootstrapOwnerOptions.SectionName)
    .Get<BootstrapOwnerOptions>() ?? new BootstrapOwnerOptions();
var authenticationOptions = builder.Configuration
    .GetSection(Legaria.Application.Configuration.AuthenticationOptions.SectionName)
    .Get<Legaria.Application.Configuration.AuthenticationOptions>()
    ?? new Legaria.Application.Configuration.AuthenticationOptions();
if (authenticationOptions.RefreshTokenDays != 7 ||
    authenticationOptions.MaximumFailedAttempts != 5 ||
    authenticationOptions.LockoutMinutes != 15 ||
    authenticationOptions.VerificationTokenHours != 24 ||
    authenticationOptions.PasswordResetTokenMinutes != 30)
{
    throw new InvalidOperationException(
        "La configuración Authentication debe conservar refresh 7 días, lockout 5/15, " +
        "verificación 24 horas y reset 30 minutos.");
}

builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton(resendOptions);
builder.Services.AddSingleton(frontendOptions);
builder.Services.AddSingleton(bootstrapOptions);
builder.Services.AddSingleton(authenticationOptions);

builder.Services.AddDbContext<LegariaDbContext>(options =>
    options
        .UseNpgsql(connectionString)
        .UseSnakeCaseNamingConvention());

builder.Services.Configure<ResendClientOptions>(options => options.ApiToken = resendOptions.ApiKey);
builder.Services.AddHttpClient<ResendClient>(client => client.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddTransient<IResend, ResendClient>();

builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddSingleton<IEmailNormalizer, EmailNormalizer>();
builder.Services.AddSingleton<ISecureTokenService, SecureTokenService>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IAccessTokenService, JwtAccessTokenService>();
builder.Services.AddSingleton<IEmailTemplateRenderer, EmailTemplateRenderer>();
builder.Services.AddSingleton<INitValidator, NitValidator>();
builder.Services.AddTransient<IEmailSender, ResendEmailSender>();
builder.Services.AddScoped<IAuthenticationRepository, AuthenticationRepository>();
builder.Services.AddScoped<IOrganizationRepository, OrganizationRepository>();
builder.Services.AddScoped<ITenantInvitationRepository, TenantInvitationRepository>();
builder.Services.AddScoped<IBranchRepository, BranchRepository>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<ITenantInvitationService, TenantInvitationService>();
builder.Services.AddScoped<IBranchService, BranchService>();
builder.Services.AddScoped<IPlatformOwnerBootstrapper, PlatformOwnerBootstrapper>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = ClaimTypes.Role
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = ValidateAccountAsync
        };
    });

builder.Services.AddAuthorization(AuthorizationPolicies.Configure);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy
            .WithOrigins(frontendUri.GetLeftPart(UriPartial.Authority))
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new
            {
                type = "about:blank",
                title = "Demasiadas solicitudes",
                status = StatusCodes.Status429TooManyRequests,
                detail = "Espera un momento antes de volver a intentarlo.",
                code = "auth.rate_limited"
            },
            cancellationToken);
    };
    options.AddPolicy("login", context =>
        CreateIpPartition(context, 5, TimeSpan.FromMinutes(5)));
    options.AddPolicy("email-request", context =>
        CreateIpPartition(context, 3, TimeSpan.FromMinutes(15)));
    options.AddPolicy("account-token", context =>
        CreateIpPartition(context, 10, TimeSpan.FromMinutes(15)));
    options.AddPolicy("refresh", context =>
        CreateIpPartition(context, 30, TimeSpan.FromMinutes(1)));
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseMiddleware<AuthExceptionHandlerMiddleware>();
app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await using (var scope = app.Services.CreateAsyncScope())
{
    var bootstrapper = scope.ServiceProvider.GetRequiredService<IPlatformOwnerBootstrapper>();
    await bootstrapper.BootstrapAsync(CancellationToken.None);
}

await app.RunAsync();

static RateLimitPartition<string> CreateIpPartition(
    HttpContext context,
    int permitLimit,
    TimeSpan window) =>
    RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = window,
            QueueLimit = 0,
            AutoReplenishment = true
        });

static bool IsValidEmail(string? value) =>
    !string.IsNullOrWhiteSpace(value) &&
    MailAddress.TryCreate(value, out var parsed) &&
    string.Equals(parsed.Address, value.Trim(), StringComparison.OrdinalIgnoreCase);

static async Task ValidateAccountAsync(TokenValidatedContext context)
{
    var principal = context.Principal;
    var repository = context.HttpContext.RequestServices.GetRequiredService<IAuthenticationRepository>();
    var userIdText = principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
    var accountType = principal?.FindFirstValue("account_type");
    var securityStamp = principal?.FindFirstValue("security_stamp");

    if (!Guid.TryParse(userIdText, out var userId) || string.IsNullOrWhiteSpace(securityStamp))
    {
        context.Fail("El token no contiene una identidad válida.");
        return;
    }

    if (accountType == AccountTypeCodes.Platform)
    {
        var user = await repository.FindPlatformByIdAsync(userId, context.HttpContext.RequestAborted);
        var expectedRole = user?.Role == PlatformRole.Owner
            ? PlatformRoleCodes.Owner
            : PlatformRoleCodes.PlatformAdmin;
        var rolesAreCurrent = user is not null &&
            principal!.FindAll(ClaimTypes.Role)
                .Select(claim => claim.Value)
                .ToHashSet(StringComparer.Ordinal)
                .SetEquals([expectedRole]);
        if (user is null ||
            user.Status != AccountStatus.Active ||
            user.EmailVerifiedAt is null ||
            !string.Equals(user.SecurityStamp, securityStamp, StringComparison.Ordinal) ||
            !rolesAreCurrent ||
            principal!.HasClaim(claim =>
                claim.Type is "organization_id" or "employee_id"))
        {
            context.Fail("La cuenta de plataforma ya no es válida.");
        }

        return;
    }

    if (accountType == AccountTypeCodes.Tenant)
    {
        var user = await repository.FindTenantByIdAsync(userId, context.HttpContext.RequestAborted);
        var organizationClaim = principal?.FindFirstValue("organization_id");
        var employeeClaim = principal?.FindFirstValue("employee_id");
        var employeeIsCurrent = user?.EmployeeId is null
            ? string.IsNullOrWhiteSpace(employeeClaim)
            : Guid.TryParse(employeeClaim, out var employeeId) && employeeId == user.EmployeeId;
        var expectedRoles = user?.Roles
            .Select(item => item.SystemRole.Code)
            .ToHashSet(StringComparer.Ordinal);
        var rolesAreCurrent = expectedRoles is not null &&
            principal!.FindAll(ClaimTypes.Role)
                .Select(claim => claim.Value)
                .ToHashSet(StringComparer.Ordinal)
                .SetEquals(expectedRoles);
        if (user is null ||
            user.Status != AccountStatus.Active ||
            user.EmailVerifiedAt is null ||
            !string.Equals(user.SecurityStamp, securityStamp, StringComparison.Ordinal) ||
            !Guid.TryParse(organizationClaim, out var organizationId) ||
            organizationId != user.OrganizationId ||
            !employeeIsCurrent ||
            !rolesAreCurrent ||
            !await repository.IsOrganizationActiveAsync(user.OrganizationId, context.HttpContext.RequestAborted))
        {
            context.Fail("La cuenta tenant ya no es válida.");
        }

        return;
    }

    context.Fail("El token no contiene un tipo de cuenta válido.");
}

public partial class Program;
