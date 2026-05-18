namespace SampleMicroservice.Host.Configurations;

internal static class Startup
{
    internal static WebApplicationBuilder AddConfigurations(this WebApplicationBuilder builder)
    {
        string env = builder.Environment.EnvironmentName;

        builder.Configuration
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true);

        const string configurationsDirectory = "Configurations";
        if (Directory.Exists(configurationsDirectory))
        {
            foreach (string config in Directory.GetFiles(configurationsDirectory, "*.json", SearchOption.TopDirectoryOnly))
            {
                builder.Configuration.AddJsonFile(config, optional: false, reloadOnChange: true);
            }

            foreach (string config in Directory.GetFiles(configurationsDirectory, $"*.{env}.json", SearchOption.TopDirectoryOnly))
            {
                builder.Configuration.AddJsonFile(config, optional: true, reloadOnChange: true);
            }
        }

        builder.Configuration.AddEnvironmentVariables();
        return builder;
    }
}
