using Legaria.Domain.Authentication;
using Legaria.Domain.Employees;
using Legaria.Domain.Tenancy;
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
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<AccountEmail> AccountEmails => Set<AccountEmail>();
    public DbSet<SystemRole> SystemRoles => Set<SystemRole>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserBranchAccess> UserBranchAccesses => Set<UserBranchAccess>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<AccountToken> AccountTokens => Set<AccountToken>();
    public DbSet<SecurityAuditEvent> SecurityAuditEvents => Set<SecurityAuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LegariaDbContext).Assembly);
    }
}
