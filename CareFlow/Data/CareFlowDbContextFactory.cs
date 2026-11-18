
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace CareFlow.Data;

public class CareFlowDbContextFactory
    : IDesignTimeDbContextFactory<CareFlowDbContext>
{
    public CareFlowDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var connectionString =
            configuration.GetConnectionString("CareFlowConnection")
            ?? throw new InvalidOperationException(
                "CareFlow database connection string is missing.");

        var options = new DbContextOptionsBuilder<CareFlowDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new CareFlowDbContext(options);
    }
}
