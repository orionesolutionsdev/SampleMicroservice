using Serilog;
using Serilog.Exceptions;
using SampleMicroservice.Application;
using SampleMicroservice.Host.Configurations;
using SampleMicroservice.Infrastructure;
using SampleMicroservice.Infrastructure.Persistence.Context;

StaticLogger.EnsureInitialized();
Log.Information("Server booting up...");

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddConfigurations();

    var serviceName = builder.Configuration["LoggerSettings:AppName"] ?? "SampleMicroservice";

    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithExceptionDetails()
        .Enrich.WithMachineName()
        .Enrich.WithProcessId()
        .Enrich.WithThreadId()
        .Enrich.WithProperty("ServiceName", serviceName)
        .Enrich.WithProperty("Environment", ctx.HostingEnvironment.EnvironmentName)
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] [{ServiceName}] [CID:{CorrelationId}] {Message:lj}{NewLine}{Exception}"));

    builder.Services.AddControllers();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddApplication();

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<SampleMicroserviceDbContext>();
        dbContext.Database.EnsureCreated();
    }

    app.UseInfrastructure(app.Configuration);
    app.MapControllers();
    app.MapHealthChecks("/health");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Unhandled exception in Host.");
}
finally
{
    Log.Information("Server shutting down...");
    Log.CloseAndFlush();
}

public static class StaticLogger
{
    public static void EnsureInitialized()
    {
        if (Log.Logger is not Serilog.Core.Logger)
        {
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateBootstrapLogger();
        }
    }
}
