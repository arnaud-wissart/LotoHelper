using Loto.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Loto.Infrastructure;

// Design-time factory to enable `dotnet ef` without needing the full host.
public class LotoDbContextFactory : IDesignTimeDbContextFactory<LotoDbContext>
{
    public LotoDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LotoDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("LOTO_DB_CONNECTIONSTRING")
                               ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=lotohelper";

        optionsBuilder.UseNpgsql(connectionString);
        return new LotoDbContext(optionsBuilder.Options);
    }
}
