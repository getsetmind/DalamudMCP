using System.Text.Json;
using Manifold.Cli;
using DalamudMCP.Protocol;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DalamudMCP.Cli;

public static class CliHttpServerRunner
{
    private const string CurrentProtocolVersion = "2025-03-26";
    private const string StreamableHttpContentType = "text/event-stream";

    public static async Task<int> RunAsync(CliRuntimeOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (options is null || string.IsNullOrWhiteSpace(options.PipeName))
            throw new InvalidOperationException("A live --pipe connection is required.");

        NamedPipeProtocolClient protocolClient = new(options.PipeName);
        DescribeOperationsResponse catalog = await protocolClient.DescribeOperationsAsync(cancellationToken).ConfigureAwait(false);
        RemoteMcpToolService toolService = new(catalog.Operations, protocolClient);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(BuildListenUrl(options));
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IProtocolOperationClient>(protocolClient);
        builder.Services.AddSingleton(toolService);
        builder.Services
            .AddMcpServer()
            .WithListToolsHandler((requestContext, ct) => toolService.ListToolsAsync(requestContext, ct))
            .WithCallToolHandler((requestContext, ct) => toolService.CallToolAsync(requestContext, ct));

        builder.Services.AddSingleton(sp =>
        {
            StreamableHttpServerTransport transport = new(sp.GetRequiredService<ILoggerFactory>())
            {
                Stateless = true
            };
            return transport;
        });

        WebApplication app = builder.Build();
        StreamableHttpServerTransport transport = app.Services.GetRequiredService<StreamableHttpServerTransport>();
        McpServerOptions serverOptions = app.Services.GetRequiredService<IOptions<McpServerOptions>>().Value;
        ILoggerFactory loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        McpServer server = McpServer.Create(transport, serverOptions, loggerFactory, app.Services);

        MapEndpoint(app, transport, options.HttpPath);

        Task serverTask = server.RunAsync(cancellationToken);
        try
        {
            await app.StartAsync(cancellationToken).ConfigureAwait(false);
            await ((IHost)app).WaitForShutdownAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            await transport.DisposeAsync().ConfigureAwait(false);
            await app.DisposeAsync().ConfigureAwait(false);
            try
            {
                await serverTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
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

    private static void MapEndpoint(
        WebApplication app,
        StreamableHttpServerTransport transport,
        string path)
    {
        string normalizedPath = string.IsNullOrWhiteSpace(path)
            ? CliRuntimeOptions.DefaultHttpPath
            : path;

        app.MapMethods(normalizedPath, ["GET"], async context =>
        {
            AddProtocolVersionHeader(context);
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            await context.Response.WriteAsync("This MCP endpoint expects POST requests.", context.RequestAborted)
                .ConfigureAwait(false);
        });

        app.MapMethods(normalizedPath, ["POST"], async context =>
        {
            AddProtocolVersionHeader(context);

            JsonRpcMessage? message = await JsonSerializer.DeserializeAsync<JsonRpcMessage>(
                    context.Request.Body,
                    cancellationToken: context.RequestAborted)
                .ConfigureAwait(false);
            if (message is null)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("The MCP request body was empty or invalid.", context.RequestAborted)
                    .ConfigureAwait(false);
                return;
            }

            await using PooledBufferStream responseBuffer = new();
            bool wroteResponse = await transport.HandlePostRequestAsync(message, responseBuffer, context.RequestAborted)
                .ConfigureAwait(false);
            context.Response.StatusCode = wroteResponse
                ? StatusCodes.Status200OK
                : StatusCodes.Status202Accepted;
            if (!wroteResponse)
                return;

            context.Response.ContentType = StreamableHttpContentType;
            context.Response.ContentLength = responseBuffer.Length;
            await context.Response.Body.WriteAsync(responseBuffer.WrittenMemory, context.RequestAborted).ConfigureAwait(false);
            await context.Response.Body.FlushAsync(context.RequestAborted).ConfigureAwait(false);
        });
    }

    private static void AddProtocolVersionHeader(HttpContext context)
    {
        context.Response.Headers["MCP-Protocol-Version"] = CurrentProtocolVersion;
    }
}