namespace Legaria.Domain.Employees;

public sealed class Employee
{
    private Employee()
    {
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string DocumentType { get; private set; } = string.Empty;
    public string DocumentNumber { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string? MobilePhone { get; private set; }
    public string? ContactEmail { get; private set; }
    public DateTimeOffset? WhatsAppConsentAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Employee Create(
        Guid organizationId,
        string documentType,
        string documentNumber,
        string firstName,
        string lastName,
        DateTimeOffset now)
    {
        return new Employee
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            DocumentType = documentType,
            DocumentNumber = documentNumber,
            FirstName = firstName,
            LastName = lastName,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void UpdateIdentity(
        string documentType,
        string documentNumber,
        string firstName,
        string lastName,
        DateTimeOffset now)
    {
        DocumentType = documentType;
        DocumentNumber = documentNumber;
        FirstName = firstName;
        LastName = lastName;
        UpdatedAt = now;
    }

    public void UpdateNotificationContact(
        string? mobilePhone,
        string? contactEmail,
        bool whatsappConsent,
        DateTimeOffset now)
    {
        MobilePhone = mobilePhone;
        ContactEmail = contactEmail;
        WhatsAppConsentAt = whatsappConsent && mobilePhone is not null
            ? WhatsAppConsentAt ?? now
            : null;
        UpdatedAt = now;
    }
}
