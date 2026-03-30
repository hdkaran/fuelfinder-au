using FuelFinder.Api.Data;
using FuelFinder.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FuelFinder.Api.Tests;

/// <summary>
/// WebApplicationFactory that swaps SQL Server for an in-memory database
/// and skips the FuelCheck station seeder so tests start with a clean slate.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    // Each factory instance gets its own isolated in-memory database.
    private readonly string _dbName = "TestDb_" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Raise rate limit so the test suite never trips it
        builder.ConfigureAppConfiguration(config =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:ReportsPerWindow"] = "1000",
            }));

        // ConfigureTestServices runs AFTER the app's ConfigureServices, so our
        // registrations here take precedence and cleanly override SQL Server.
        builder.ConfigureTestServices(services =>
        {
            // Remove all DbContext-related descriptors registered by the app.
            // In EF Core 10 this includes DbContextOptions<T> and the
            // IDbContextOptionsConfiguration<T> that carries the SQL provider.
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(AppDbContext) ||
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GetGenericTypeDefinition().FullName?
                         .Contains("IDbContextOptionsConfiguration") == true &&
                     d.ServiceType.GenericTypeArguments.Contains(typeof(AppDbContext))))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            // Remove seeder so tests start with a clean, empty database.
            services.RemoveAll<StationSeeder>();
        });
    }
}
