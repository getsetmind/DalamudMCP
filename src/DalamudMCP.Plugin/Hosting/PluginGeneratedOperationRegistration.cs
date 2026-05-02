using Manifold;
using Manifold.Generated;
using DalamudMCP.Plugin.Readers;
using Microsoft.Extensions.DependencyInjection;

namespace DalamudMCP.Plugin.Hosting;

public static class PluginGeneratedOperationRegistration
{
    public static IServiceCollection AddGeneratedPluginOperations(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        foreach (Type operationType in GeneratedOperationRegistry.Operations
                     .Select(static descriptor => descriptor.DeclaringType)
                     .Distinct())
        {
            services.AddSingleton(operationType);
            RegisterFormatter(services, operationType);
            if (!typeof(IPluginReaderStatus).IsAssignableFrom(operationType))
                continue;

            services.AddSingleton(typeof(IPluginReaderStatus), provider =>
                (IPluginReaderStatus)provider.GetRequiredService(operationType));
        }

        return services;
    }

    private static void RegisterFormatter(IServiceCollection services, Type operationType)
    {
        ResultFormatterAttribute? formatterAttribute = operationType
            .GetCustomAttributes(typeof(ResultFormatterAttribute), inherit: false)
            .OfType<ResultFormatterAttribute>()
            .SingleOrDefault();
        if (formatterAttribute?.FormatterType is null)
            return;

        services.AddSingleton(formatterAttribute.FormatterType);
    }
}