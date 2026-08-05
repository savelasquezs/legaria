using Legaria.Domain.Authentication;

namespace Legaria.Domain.Tenancy;

public sealed class Branch
{
    private Branch()
    {
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string? ContactEmail { get; private set; }
    public string? Phone { get; private set; }
    public string Address { get; private set; } = string.Empty;
    public string MunicipalityCode { get; private set; } = string.Empty;
    public BranchStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Branch Create(
        Guid organizationId,
        string name,
        string normalizedName,
        string? contactEmail,
        string? phone,
        string address,
        string municipalityCode,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name,
            NormalizedName = normalizedName,
            ContactEmail = contactEmail,
            Phone = phone,
            Address = address,
            MunicipalityCode = municipalityCode,
            Status = BranchStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

    public void Update(
        string name,
        string normalizedName,
        string? contactEmail,
        string? phone,
        string address,
        string municipalityCode,
        DateTimeOffset now)
    {
        Name = name;
        NormalizedName = normalizedName;
        ContactEmail = contactEmail;
        Phone = phone;
        Address = address;
        MunicipalityCode = municipalityCode;
        UpdatedAt = now;
    }

    public bool Deactivate(DateTimeOffset now)
    {
        if (Status == BranchStatus.Inactive)
        {
            return false;
        }

        Status = BranchStatus.Inactive;
        UpdatedAt = now;
        return true;
    }

    public bool Reactivate(DateTimeOffset now)
    {
        if (Status == BranchStatus.Active)
        {
            return false;
        }

        Status = BranchStatus.Active;
        UpdatedAt = now;
        return true;
    }
}
