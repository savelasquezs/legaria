using Legaria.Domain.Authentication;

namespace Legaria.Domain.Tenancy;

public sealed class Organization
{
    private Organization()
    {
    }

    public Guid Id { get; private set; }
    public string TradeName { get; private set; } = string.Empty;
    public string LegalName { get; private set; } = string.Empty;
    public string Nit { get; private set; } = string.Empty;
    public int VerificationDigit { get; private set; }
    public string ContactEmail { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public string MunicipalityCode { get; private set; } = string.Empty;
    public OrganizationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Organization Create(
        string tradeName,
        string legalName,
        string nit,
        int verificationDigit,
        string contactEmail,
        string phone,
        string address,
        string municipalityCode,
        DateTimeOffset now)
    {
        return new Organization
        {
            Id = Guid.NewGuid(),
            TradeName = tradeName,
            LegalName = legalName,
            Nit = nit,
            VerificationDigit = verificationDigit,
            ContactEmail = contactEmail,
            Phone = phone,
            Address = address,
            MunicipalityCode = municipalityCode,
            Status = OrganizationStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Update(
        string tradeName,
        string legalName,
        string nit,
        int verificationDigit,
        string contactEmail,
        string phone,
        string address,
        string municipalityCode,
        DateTimeOffset now)
    {
        TradeName = tradeName;
        LegalName = legalName;
        Nit = nit;
        VerificationDigit = verificationDigit;
        ContactEmail = contactEmail;
        Phone = phone;
        Address = address;
        MunicipalityCode = municipalityCode;
        UpdatedAt = now;
    }

    public bool Suspend(DateTimeOffset now)
    {
        if (Status == OrganizationStatus.Suspended)
        {
            return false;
        }

        Status = OrganizationStatus.Suspended;
        UpdatedAt = now;
        return true;
    }

    public bool Reactivate(DateTimeOffset now)
    {
        if (Status == OrganizationStatus.Active)
        {
            return false;
        }

        Status = OrganizationStatus.Active;
        UpdatedAt = now;
        return true;
    }
}
