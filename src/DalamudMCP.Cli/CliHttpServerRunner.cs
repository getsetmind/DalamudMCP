using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using DalamudMCP.Protocol;
using Manifold.Cli;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DalamudMCP.Cli;

public static class CliHttpServerRunner
{
    public static async Task<int> RunAsync(CliRuntimeOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (options is null || string.IsNullOrWhiteSpace(options.PipeName))
            throw new InvalidOperationException("A live --pipe connection is required.");

        string? bearerToken = Environment.GetEnvironmentVariable(ProtocolContract.HttpBearerTokenEnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            throw new InvalidOperationException(
                $"Set {ProtocolContract.HttpBearerTokenEnvironmentVariableName} before starting the HTTP server.");
        }

        NamedPipeProtocolClient protocolClient = new(options.PipeName);
        DescribeOperationsResponse catalog = await protocolClient.DescribeOperationsAsync(cancellationToken).ConfigureAwait(false);
        RemoteMcpToolService toolService = new(catalog.Operations, protocolClient);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(BuildListenUrl(options));
        builder.Logging.ClearProviders();
        builder.Services.AddHostFiltering(hostOptions =>
        {
            hostOptions.AllowedHosts = ["127.0.0.1", "localhost", "[::1]"];
        });
        builder.Services.AddSingleton<IProtocolOperationClient>(protocolClient);
        builder.Services.AddSingleton(toolService);
        builder.Services
            .AddMcpServer()
            .WithHttpTransport(transportOptions => transportOptions.Stateless = true)
            .WithListToolsHandler((requestContext, ct) => toolService.ListToolsAsync(requestContext, ct))
            .WithCallToolHandler((requestContext, ct) => toolService.CallToolAsync(requestContext, ct));

        await using WebApplication app = builder.Build();
        app.UseHostFiltering();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.Equals(options.HttpPath) &&
                !IsAllowedOrigin(context.Request.Headers.Origin))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("The request Origin is not allowed.", context.RequestAborted)
                    .ConfigureAwait(false);
                return;
            }

            if (context.Request.Path.Equals(options.HttpPath) &&
                !IsAuthorized(context.Request.Headers.Authorization, bearerToken))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.WWWAuthenticate = "Bearer";
                await context.Response.WriteAsync("A valid bearer token is required.", context.RequestAborted)
                    .ConfigureAwait(false);
                return;
            }

            await next(context).ConfigureAwait(false);
        });
        app.MapMcp(options.HttpPath);

        try
        {
            await app.RunAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }

        return CliExitCodes.Success;
    }

    public static string BuildListenUrl(CliRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return $"http://127.0.0.1:{options.HttpPort}";
    }

    public static string BuildEndpointUrl(CliRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return $"{BuildListenUrl(options)}{options.HttpPath}";
    }

    internal static bool IsAllowedOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
            return true;

        return Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri) &&
               uri.IsLoopback &&
               (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool IsAuthorized(string? authorizationHeader, string? configuredToken)
    {
        if (string.IsNullOrWhiteSpace(configuredToken))
            return false;
        if (!AuthenticationHeaderValue.TryParse(authorizationHeader, out AuthenticationHeaderValue? authorization) ||
            !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(authorization.Parameter))
        {
            return false;
        }

        byte[] expected = Encoding.UTF8.GetBytes(configuredToken);
        byte[] actual = Encoding.UTF8.GetBytes(authorization.Parameter);
        return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
