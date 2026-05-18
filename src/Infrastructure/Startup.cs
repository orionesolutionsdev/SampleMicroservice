using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SampleMicroservice.Application.Common.Interfaces;
using SampleMicroservice.Application.Common.Persistence;
using SampleMicroservice.Infrastructure.Auth;
using SampleMicroservice.Infrastructure.Http;
using SampleMicroservice.Infrastructure.Middleware;
using SampleMicroservice.Infrastructure.Persistence.Context;
using SampleMicroservice.Infrastructure.Persistence.Repository;
using SampleMicroservice.Infrastructure.Validations;

namespace SampleMicroservice.Infrastructure;

public static class Startup
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

        services
            .AddTransient<ExceptionMiddleware>()
            .AddTransient<CorrelationIdMiddleware>()
            .AddTransient<CorrelationIdDelegatingHandler>()
            .AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddHealthChecks();

        services.AddOpenApiDocument(doc =>
        {
            doc.Title = "Sample Microservice API";
            doc.Version = "v1";
            doc.Description = "Sample microservice following the TOA API Clean Architecture pattern.";
        });

        services
            .AddObservability(config)
            .AddPersistence(config)
            .AddCurrentUser()
            .AddApplicationServices();

        return services;
    }

    private static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration config)
    {
        var serviceName = config["LoggerSettings:AppName"] ?? "SampleMicroservice";
        var otlpEndpoint = config["OpenTelemetry:Endpoint"];

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(o =>
                    {
                        o.RecordException = true;
                        o.EnrichWithHttpRequest = (activity, request) =>
                            activity.SetTag("correlation.id",
                                request.Headers[CorrelationIdMiddleware.HeaderName].FirstOrDefault());
                    })
                    .AddHttpClientInstrumentation();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
            });

        return services;
    }

    private static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<SampleMicroserviceDbContext>(o =>
                o.UseInMemoryDatabase("SampleMicroserviceDb"));
        }
        else
        {
            services.AddDbContext<SampleMicroserviceDbContext>(o =>
                o.UseSqlServer(connectionString));
        }

        services.AddScoped(typeof(IRepository<>), typeof(SampleMicroserviceRepository<>));
        services.AddScoped(typeof(IReadRepository<>), typeof(SampleMicroserviceRepository<>));

        return services;
    }

    private static IServiceCollection AddCurrentUser(this IServiceCollection services) =>
        services
            .AddHttpContextAccessor()
            .AddScoped<ICurrentUser, CurrentUser>();

    private static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = typeof(Startup).Assembly;
        var types = assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract).ToList();

        foreach (var type in types.Where(t => typeof(IScopedService).IsAssignableFrom(t)))
        {
            foreach (var iface in type.GetInterfaces().Where(i => i != typeof(IScopedService)))
                services.AddScoped(iface, type);
        }

        foreach (var type in types.Where(t => typeof(ITransientService).IsAssignableFrom(t)))
        {
            foreach (var iface in type.GetInterfaces().Where(i => i != typeof(ITransientService)))
                services.AddTransient(iface, type);
        }

        return services;
    }

    // Middleware order matters:
    // CorrelationId first → pushes CorrelationId into Serilog context for ALL downstream logs
    // ExceptionMiddleware second → error logs automatically include CorrelationId
    public static IApplicationBuilder UseInfrastructure(this IApplicationBuilder builder, IConfiguration config) =>
        builder
            .UseMiddleware<CorrelationIdMiddleware>()
            .UseMiddleware<ExceptionMiddleware>()
            .UseRouting()
            .UseAuthentication()
            .UseAuthorization()
            .UseOpenApi()
            .UseSwaggerUi();
}
