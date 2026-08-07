using Legaria.Domain.Authentication;
using Legaria.Domain.Employees;
using Legaria.Domain.Documents;
using Legaria.Domain.Tenancy;
using Legaria.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Legaria.Infrastructure.Persistence;

public sealed class LegariaDbContext(DbContextOptions<LegariaDbContext> options) : DbContext(options)
{
    public DbSet<PlatformUser> PlatformUsers => Set<PlatformUser>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Municipality> Municipalities => Set<Municipality>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmploymentRelationship> EmploymentRelationships => Set<EmploymentRelationship>();
    public DbSet<JobPosition> JobPositions => Set<JobPosition>();
    public DbSet<JobPositionDocumentRequirement> JobPositionDocumentRequirements => Set<JobPositionDocumentRequirement>();
    public DbSet<EmployeeAssignment> EmployeeAssignments => Set<EmployeeAssignment>();
    public DbSet<DocumentCategory> DocumentCategories => Set<DocumentCategory>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();
    public DbSet<EmployeeDocumentEvidence> EmployeeDocumentEvidences => Set<EmployeeDocumentEvidence>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<AccountEmail> AccountEmails => Set<AccountEmail>();
    public DbSet<SystemRole> SystemRoles => Set<SystemRole>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserBranchAccess> UserBranchAccesses => Set<UserBranchAccess>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<AccountToken> AccountTokens => Set<AccountToken>();
    public DbSet<SecurityAuditEvent> SecurityAuditEvents => Set<SecurityAuditEvent>();
    public DbSet<WhatsAppChannel> WhatsAppChannels => Set<WhatsAppChannel>();
    public DbSet<WhatsAppTemplate> WhatsAppTemplates => Set<WhatsAppTemplate>();
    public DbSet<NotificationRule> NotificationRules => Set<NotificationRule>();
    public DbSet<NotificationRuleSchedule> NotificationRuleSchedules => Set<NotificationRuleSchedule>();
    public DbSet<NotificationEvent> NotificationEvents => Set<NotificationEvent>();
    public DbSet<NotificationQueueItem> NotificationQueueItems => Set<NotificationQueueItem>();
    public DbSet<NotificationDeliveryAttempt> NotificationDeliveryAttempts => Set<NotificationDeliveryAttempt>();
    public DbSet<WhatsAppWebhookReceipt> WhatsAppWebhookReceipts => Set<WhatsAppWebhookReceipt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LegariaDbContext).Assembly);
    }
}
