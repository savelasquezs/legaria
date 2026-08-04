namespace Legaria.Domain.Tenancy;

public sealed class Department
{
    private Department()
    {
    }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
}

public sealed class Municipality
{
    private Municipality()
    {
    }

    public string Code { get; private set; } = string.Empty;
    public string DepartmentCode { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Type { get; private set; } = string.Empty;
    public Department Department { get; private set; } = null!;
}
