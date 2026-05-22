using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NotesApi.Data;

/// <summary>
/// Used by `dotnet ef` tooling so migrations are always generated against PostgreSQL.
/// This ensures column types like uuid, boolean, and timestamptz are used instead of
/// SQLite's TEXT/INTEGER fallbacks.
///
/// Requires the postgres container to be running:
///   docker compose up postgres -d
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? "Host=localhost;Port=5432;Database=ttrpg;Username=ttrpg;Password=ttrpg_dev";

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsAssembly("NotesApi"));

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
