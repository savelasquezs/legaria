using Legaria.Domain.Authentication;
using Legaria.Domain.Employees;
using Legaria.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Legaria.Infrastructure.Persistence;

internal sealed class PlatformUserConfiguration : IEntityTypeConfiguration<PlatformUser>
{
    public void Configure(EntityTypeBuilder<PlatformUser> builder)
    {
        builder.ToTable("platform_users", table =>
        {
            table.HasCheckConstraint("ck_platform_users_role", "role IN ('OWNER', 'PLATFORM_ADMIN')");
            table.HasCheckConstraint("ck_platform_users_status", "status IN ('ACTIVE', 'SUSPENDED')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).HasMaxLength(320).IsRequired();
        builder.Property(x => x.NormalizedEmail).HasMaxLength(320).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Role).HasConversion(
            value => value == PlatformRole.Owner ? PlatformRoleCodes.Owner : PlatformRoleCodes.PlatformAdmin,
            value => value == PlatformRoleCodes.Owner ? PlatformRole.Owner : PlatformRole.PlatformAdmin);
        builder.Property(x => x.Status).HasConversion(
            value => value == AccountStatus.Active ? "ACTIVE" : "SUSPENDED",
            value => value == "ACTIVE" ? AccountStatus.Active : AccountStatus.Suspended);
        builder.Property(x => x.SecurityStamp).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.NormalizedEmail).IsUnique();
        builder.HasIndex(x => x.Role)
            .IsUnique()
            .HasFilter("\"role\" = 'OWNER'");
    }
}

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations", table =>
            table.HasCheckConstraint("ck_organizations_status", "status IN ('ACTIVE', 'SUSPENDED')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasConversion(
            value => value == OrganizationStatus.Active ? "ACTIVE" : "SUSPENDED",
            value => value == "ACTIVE" ? OrganizationStatus.Active : OrganizationStatus.Suspended);
    }
}

internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("employees");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        builder.Property(x => x.DocumentType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.DocumentNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => new { x.OrganizationId, x.DocumentType, x.DocumentNumber }).IsUnique();
        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("user_accounts", table =>
            table.HasCheckConstraint("ck_user_accounts_status", "status IN ('ACTIVE', 'SUSPENDED')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).HasMaxLength(320).IsRequired();
        builder.Property(x => x.NormalizedEmail).HasMaxLength(320).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Status).HasConversion(
            value => value == AccountStatus.Active ? "ACTIVE" : "SUSPENDED",
            value => value == "ACTIVE" ? AccountStatus.Active : AccountStatus.Suspended);
        builder.Property(x => x.SecurityStamp).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.NormalizedEmail).IsUnique();
        builder.HasIndex(x => new { x.OrganizationId, x.Id }).IsUnique();
        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.EmployeeId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Roles).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SystemRoleConfiguration : IEntityTypeConfiguration<SystemRole>
{
    public void Configure(EntityTypeBuilder<SystemRole> builder)
    {
        builder.ToTable("system_roles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasData(
            new { Id = SystemRole.SuperAdminId, Code = SystemRoleCodes.SuperAdmin, Name = "Superadministrador" },
            new { Id = SystemRole.BranchAdminId, Code = SystemRoleCodes.BranchAdmin, Name = "Administrador de sucursal" });
    }
}

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");
        builder.HasKey(x => new { x.UserAccountId, x.SystemRoleId });
        builder.HasOne(x => x.SystemRole)
            .WithMany()
            .HasForeignKey(x => x.SystemRoleId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>()
            .WithMany(x => x.Roles)
            .HasForeignKey(x => x.UserAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RefreshSessionConfiguration : IEntityTypeConfiguration<RefreshSession>
{
    public void Configure(EntityTypeBuilder<RefreshSession> builder)
    {
        builder.ToTable("refresh_sessions", table =>
            table.HasCheckConstraint(
                "ck_refresh_sessions_single_account",
                "num_nonnulls(platform_user_id, user_account_id) = 1"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CreatedByIp).HasMaxLength(64);
        builder.Property(x => x.RevokedByIp).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(512);
        builder.Property(x => x.RevocationReason).HasMaxLength(100);
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.FamilyId);
        builder.HasIndex(x => new { x.PlatformUserId, x.ExpiresAt });
        builder.HasIndex(x => new { x.UserAccountId, x.ExpiresAt });
        builder.HasOne<PlatformUser>()
            .WithMany()
            .HasForeignKey(x => x.PlatformUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.UserAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RefreshSession>()
            .WithMany()
            .HasForeignKey(x => x.ReplacedBySessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AccountTokenConfiguration : IEntityTypeConfiguration<AccountToken>
{
    public void Configure(EntityTypeBuilder<AccountToken> builder)
    {
        builder.ToTable("account_tokens", table =>
        {
            table.HasCheckConstraint(
                "ck_account_tokens_single_account",
                "num_nonnulls(platform_user_id, user_account_id) = 1");
            table.HasCheckConstraint(
                "ck_account_tokens_account_type",
                "(account_type = 'PLATFORM' AND platform_user_id IS NOT NULL AND user_account_id IS NULL) OR " +
                "(account_type = 'TENANT' AND user_account_id IS NOT NULL AND platform_user_id IS NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AccountType).HasConversion(
            value => value == AccountType.Platform ? AccountTypeCodes.Platform : AccountTypeCodes.Tenant,
            value => value == AccountTypeCodes.Platform ? AccountType.Platform : AccountType.Tenant);
        builder.Property(x => x.Purpose).HasConversion(
            value => value == AccountTokenPurpose.EmailVerification ? "EMAIL_VERIFICATION" : "PASSWORD_RESET",
            value => value == "EMAIL_VERIFICATION"
                ? AccountTokenPurpose.EmailVerification
                : AccountTokenPurpose.PasswordReset);
        builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CreatedByIp).HasMaxLength(64);
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.PlatformUserId, x.Purpose, x.ExpiresAt });
        builder.HasIndex(x => new { x.UserAccountId, x.Purpose, x.ExpiresAt });
        builder.HasOne<PlatformUser>()
            .WithMany()
            .HasForeignKey(x => x.PlatformUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.UserAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SecurityAuditEventConfiguration : IEntityTypeConfiguration<SecurityAuditEvent>
{
    public void Configure(EntityTypeBuilder<SecurityAuditEvent> builder)
    {
        builder.ToTable("security_audit_events");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Outcome).HasMaxLength(50).IsRequired();
        builder.Property(x => x.AccountType).HasConversion(
            value => value == null ? null : value == AccountType.Platform ? AccountTypeCodes.Platform : AccountTypeCodes.Tenant,
            value => value == null ? null : value == AccountTypeCodes.Platform ? AccountType.Platform : AccountType.Tenant);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(512);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.PlatformUserId);
        builder.HasIndex(x => x.UserAccountId);
        builder.HasOne<PlatformUser>()
            .WithMany()
            .HasForeignKey(x => x.PlatformUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.UserAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
