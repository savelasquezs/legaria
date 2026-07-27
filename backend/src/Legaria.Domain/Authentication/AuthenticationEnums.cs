namespace Legaria.Domain.Authentication;

public enum AccountType
{
    Platform,
    Tenant
}

public enum PlatformRole
{
    Owner,
    PlatformAdmin
}

public enum AccountStatus
{
    Active,
    Suspended
}

public enum OrganizationStatus
{
    Active,
    Suspended
}

public enum AccountTokenPurpose
{
    EmailVerification,
    PasswordReset
}

public static class SystemRoleCodes
{
    public const string SuperAdmin = "SUPER_ADMIN";
    public const string BranchAdmin = "BRANCH_ADMIN";
}

public static class PlatformRoleCodes
{
    public const string Owner = "OWNER";
    public const string PlatformAdmin = "PLATFORM_ADMIN";
}

public static class AccountTypeCodes
{
    public const string Platform = "PLATFORM";
    public const string Tenant = "TENANT";
}
