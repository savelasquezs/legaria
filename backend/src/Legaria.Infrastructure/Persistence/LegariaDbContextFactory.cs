using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Legaria.Infrastructure.Persistence;

public sealed class LegariaDbContextFactory : IDesignTimeDbContextFactory<LegariaDbContext>
{
    public LegariaDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Database=legaria;Username=postgres";

        var options = new DbContextOptionsBuilder<LegariaDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new LegariaDbContext(options);
    }
}
