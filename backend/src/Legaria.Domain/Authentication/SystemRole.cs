namespace Legaria.Domain.Authentication;

public sealed class SystemRole
{
    private SystemRole()
    {
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;

    public static readonly Guid SuperAdminId = Guid.Parse("a4ee7a2e-c508-4c67-9132-877d600d74d2");
    public static readonly Guid BranchAdminId = Guid.Parse("ca3759ba-98b6-4de0-b3a7-44ef0f274e87");
}

public sealed class UserRole
{
    private UserRole()
    {
    }

    public Guid UserAccountId { get; private set; }
    public Guid SystemRoleId { get; private set; }
    public SystemRole SystemRole { get; private set; } = null!;

    public static UserRole Create(Guid userAccountId, Guid systemRoleId)
    {
        return new UserRole
        {
            UserAccountId = userAccountId,
            SystemRoleId = systemRoleId
        };
    }
}
