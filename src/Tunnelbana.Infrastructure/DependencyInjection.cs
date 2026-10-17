using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tunnelbana.Application;
namespace Tunnelbana.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection s, IConfiguration c)
    {
        s.AddDbContext<MetroDbContext>(o => o.UseNpgsql(c.GetConnectionString("Postgres"), n => n.UseNetTopologySuite()));
        s.AddScoped<IMetroReadRepository, MetroReadRepository>();

        if (!string.IsNullOrWhiteSpace(c.GetConnectionString("Redis")))
            s.AddStackExchangeRedisCache(o => o.Configuration = c.GetConnectionString("Redis"));
        else s.AddDistributedMemoryCache();

        s.Configure<EtlOptions>(c.GetSection("Etl"));
        s.Configure<ProjectionOptions>(c.GetSection("Projection"));

        s.AddHttpClient<ISlSitesClient, SlSitesClient>(h => h.BaseAddress = new Uri(c["External:SlBaseUrl"] ?? "https://transport.integration.sl.se/"))
            .AddExternalApiResilience("sl", permitsPerSecond: 5);
        s.AddHttpClient<IElevationProvider, OpenElevationProvider>(h => h.BaseAddress = new Uri(c["External:ElevationBaseUrl"] ?? "https://api.open-elevation.com/"))
            .AddExternalApiResilience("dem", permitsPerSecond: 1);

        s.AddScoped<MetroEtlPipeline>();
        s.AddHostedService<MetroEtlWorker>();
        return s;
    }
}
