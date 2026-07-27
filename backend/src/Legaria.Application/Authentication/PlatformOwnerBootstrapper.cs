using System.ComponentModel.DataAnnotations;
using Legaria.Application.Configuration;
using Legaria.Domain.Authentication;

namespace Legaria.Application.Authentication;

public sealed class PlatformOwnerBootstrapper(
    IAuthenticationRepository repository,
    IEmailNormalizer emailNormalizer,
    IPasswordService passwordService,
    ISecureTokenService secureTokenService,
    IClock clock,
    BootstrapOwnerOptions options) : IPlatformOwnerBootstrapper
{
    public async Task BootstrapAsync(CancellationToken cancellationToken)
    {
        if (await repository.AnyPlatformUserAsync(cancellationToken))
        {
            return;
        }

        ValidateOptions();
        var normalizedEmail = emailNormalizer.Normalize(options.Email);
        if (await repository.EmailExistsAsync(normalizedEmail, cancellationToken))
        {
            throw new InvalidOperationException(
                "BootstrapOwner__Email ya pertenece a otra cuenta de Legaria.");
        }

        var now = clock.UtcNow;
        var owner = PlatformUser.CreateOwner(
            options.Email.Trim(),
            normalizedEmail,
            passwordService.Hash(options.Password),
            options.FirstName.Trim(),
            options.LastName.Trim(),
            secureTokenService.GenerateSecurityStamp(),
            now);
        repository.AddPlatformUser(owner);
        repository.AddAuditEvent(SecurityAuditEvent.Create(
            "PLATFORM_OWNER_BOOTSTRAPPED",
            "SUCCESS",
            now,
            AccountType.Platform,
            platformUserId: owner.Id));
        await repository.SaveChangesAsync(cancellationToken);
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(options.Email) ||
            !new EmailAddressAttribute().IsValid(options.Email) ||
            string.IsNullOrWhiteSpace(options.FirstName) ||
            string.IsNullOrWhiteSpace(options.LastName))
        {
            throw new InvalidOperationException(
                "Las variables BootstrapOwner__Email, BootstrapOwner__FirstName y " +
                "BootstrapOwner__LastName son obligatorias cuando no existe un PlatformUser.");
        }

        if (options.Password.Length is < 8 or > 128)
        {
            throw new InvalidOperationException(
                "BootstrapOwner__Password debe tener entre 8 y 128 caracteres.");
        }
    }
}
