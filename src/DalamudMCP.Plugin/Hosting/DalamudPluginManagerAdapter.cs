using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using Dalamud.Plugin;
using MemoryPack;

namespace DalamudMCP.Plugin.Hosting;

public interface IPluginManagerAdapter
{
    public ValueTask<PluginManagementResult> ExecuteLifecycleAsync(
        string pluginName,
        string action,
        bool dryRun,
        CancellationToken cancellationToken);

    public ValueTask<PluginManagementResult> ExecutePackageAsync(
        string pluginName,
        string action,
        bool useTesting,
        bool dryRun,
        bool supervisorMode,
        CancellationToken cancellationToken);
}

[MemoryPackable]
public sealed partial record PluginManagementResult(
    string PluginName,
    string Action,
    bool Success,
    string Status,
    bool DryRun,
    string? PreviousState,
    string? CurrentState,
    string? Version,
    string? Repository,
    string? ErrorMessage,
    string SummaryText,
    string? PackageSha256 = null,
    string? SourceUrl = null);

internal sealed class DalamudPluginManagerAdapter : IPluginManagerAdapter
{
    private const int SupportedDalamudApiLevel = 15;
    private readonly string currentPluginName;
    private readonly Assembly dalamudAssembly;
    private readonly Lazy<object> pluginManager;

    public DalamudPluginManagerAdapter(IDalamudPluginInterface pluginInterface)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        currentPluginName = pluginInterface.InternalName;
        dalamudAssembly = typeof(IDalamudPluginInterface).Assembly;
        pluginManager = new Lazy<object>(ResolvePluginManager, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public async ValueTask<PluginManagementResult> ExecuteLifecycleAsync(
        string pluginName,
        string action,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        string normalizedName = pluginName?.Trim() ?? string.Empty;
        string normalizedAction = action?.Trim().ToLowerInvariant() ?? string.Empty;
        PluginManagementResult? validation = Validate(normalizedName, normalizedAction, LifecycleActions);
        if (validation is not null)
            return validation;
        if (string.Equals(normalizedName, currentPluginName, StringComparison.OrdinalIgnoreCase))
            return Failure(normalizedName, normalizedAction, dryRun, "self_management_blocked", "DalamudMCP cannot manage its own lifecycle from inside the plugin process.");

        try
        {
            EnsureSupportedDalamud();
            object manager = pluginManager.Value;
            object? plugin = FindInstalledPlugin(manager, normalizedName);
            if (plugin is null)
                return Failure(normalizedName, normalizedAction, dryRun, "plugin_not_found", $"Plugin '{normalizedName}' was not found.");

            string previousState = GetPluginState(plugin) ?? "Unknown";
            if (dryRun)
            {
                return Success(
                    normalizedName,
                    normalizedAction,
                    dryRun: true,
                    "dry_run",
                    previousState,
                    previousState,
                    GetVersion(plugin),
                    null,
                    $"Validated lifecycle action '{normalizedAction}' for {normalizedName}; no change was made.");
            }

            switch (normalizedAction)
            {
                case "load":
                    await InvokeTaskAsync(
                        plugin,
                        "LoadAsync",
                        cancellationToken,
                        CreateEnum("Dalamud.Plugin.PluginLoadReason", "Reload"),
                        false,
                        cancellationToken).ConfigureAwait(false);
                    break;
                case "unload":
                    await InvokeTaskAsync(
                        plugin,
                        "UnloadAsync",
                        cancellationToken,
                        CreateEnum("Dalamud.Plugin.Internal.Types.PluginLoaderDisposalMode", "WaitBeforeDispose")).ConfigureAwait(false);
                    break;
                case "reload":
                    await InvokeTaskAsync(plugin, "ReloadAsync", cancellationToken).ConfigureAwait(false);
                    break;
                case "enable":
                    await SetDefaultProfileStateAsync(manager, plugin, enabled: true, cancellationToken).ConfigureAwait(false);
                    break;
                case "disable":
                    await SetDefaultProfileStateAsync(manager, plugin, enabled: false, cancellationToken).ConfigureAwait(false);
                    break;
            }

            string currentState = GetPluginState(plugin) ?? "Unknown";
            return Success(
                normalizedName,
                normalizedAction,
                dryRun: false,
                "completed",
                previousState,
                currentState,
                GetVersion(plugin),
                null,
                $"Lifecycle action '{normalizedAction}' completed for {normalizedName}.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Exception actual = Unwrap(exception);
            return Failure(normalizedName, normalizedAction, dryRun, "operation_failed", actual.Message);
        }
    }

    public async ValueTask<PluginManagementResult> ExecutePackageAsync(
        string pluginName,
        string action,
        bool useTesting,
        bool dryRun,
        bool supervisorMode,
        CancellationToken cancellationToken)
    {
        string normalizedName = pluginName?.Trim() ?? string.Empty;
        string normalizedAction = action?.Trim().ToLowerInvariant() ?? string.Empty;
        PluginManagementResult? validation = Validate(normalizedName, normalizedAction, PackageActions);
        if (validation is not null)
            return validation;
        if (string.Equals(normalizedName, currentPluginName, StringComparison.OrdinalIgnoreCase) && !supervisorMode)
            return Failure(normalizedName, normalizedAction, dryRun, "self_management_blocked", "DalamudMCP cannot modify its own package from inside the plugin process.");

        try
        {
            EnsureSupportedDalamud();
            object manager = pluginManager.Value;
            object? installedPlugin = FindInstalledPlugin(manager, normalizedName);
            return normalizedAction switch
            {
                "install" => await InstallAsync(manager, installedPlugin, normalizedName, useTesting, dryRun, cancellationToken).ConfigureAwait(false),
                "update" => await UpdateAsync(manager, installedPlugin, normalizedName, dryRun, cancellationToken).ConfigureAwait(false),
                "uninstall" => await UninstallAsync(manager, installedPlugin, normalizedName, dryRun, cancellationToken).ConfigureAwait(false),
                _ => Failure(normalizedName, normalizedAction, dryRun, "validation_failed", "Unsupported package action."),
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Exception actual = Unwrap(exception);
            return Failure(normalizedName, normalizedAction, dryRun, "operation_failed", actual.Message);
        }
    }

    private static readonly HashSet<string> LifecycleActions = new(StringComparer.Ordinal)
    {
        "load",
        "unload",
        "reload",
        "enable",
        "disable",
    };

    private static readonly HashSet<string> PackageActions = new(StringComparer.Ordinal)
    {
        "install",
        "update",
        "uninstall",
    };

    private object ResolvePluginManager()
    {
        Type managerType = GetRequiredType("Dalamud.Plugin.Internal.PluginManager");
        Type openServiceType = GetRequiredType("Dalamud.Service`1");
        Type serviceType = openServiceType.MakeGenericType(managerType);
        MethodInfo getMethod = serviceType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException(serviceType.FullName, "Get");
        return getMethod.Invoke(null, null)
            ?? throw new InvalidOperationException("Dalamud PluginManager service is unavailable.");
    }

    private void EnsureSupportedDalamud()
    {
        int actualApiLevel = dalamudAssembly.GetName().Version?.Major ?? 0;
        if (actualApiLevel != SupportedDalamudApiLevel)
        {
            throw new NotSupportedException(
                $"Plugin management reflection supports Dalamud API {SupportedDalamudApiLevel}, but API {actualApiLevel} is loaded.");
        }
    }

    private async Task<PluginManagementResult> InstallAsync(
        object manager,
        object? installedPlugin,
        string pluginName,
        bool useTesting,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        if (installedPlugin is not null)
            return Failure(pluginName, "install", dryRun, "already_installed", $"Plugin '{pluginName}' is already installed.");

        object? manifest = FindByInternalName(GetEnumerableProperty(manager, "AvailablePlugins"), pluginName);
        if (manifest is null)
            return Failure(pluginName, "install", dryRun, "package_not_found", $"Plugin '{pluginName}' was not found in available repositories.");

        string? version = GetOptionalProperty(manifest, useTesting ? "TestingAssemblyVersion" : "AssemblyVersion")?.ToString();
        string? repository = GetOptionalProperty(manifest, "RepoUrl")?.ToString();
        string? sourceUrl = GetOptionalProperty(manifest, useTesting ? "DownloadLinkTesting" : "DownloadLinkInstall")?.ToString();
        if (dryRun)
        {
            return Success(pluginName, "install", true, "dry_run", null, null, version, repository,
                $"Validated install of {pluginName} {version}; no change was made.", sourceUrl: sourceUrl);
        }

        await InvokeTaskAsync(
            manager,
            "InstallPluginAsync",
            cancellationToken,
            manifest,
            useTesting,
            CreateEnum("Dalamud.Plugin.PluginLoadReason", "Installer")).ConfigureAwait(false);
        object? installed = FindInstalledPlugin(manager, pluginName);
        return Success(pluginName, "install", false, "completed", null, GetPluginState(installed), version, repository,
            $"Installed {pluginName} {version}.", ComputePluginHash(installed), sourceUrl);
    }

    private static async Task<PluginManagementResult> UpdateAsync(
        object manager,
        object? installedPlugin,
        string pluginName,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        if (installedPlugin is null)
            return Failure(pluginName, "update", dryRun, "plugin_not_found", $"Plugin '{pluginName}' is not installed.");

        object? update = GetEnumerableProperty(manager, "UpdatablePlugins")
            .Cast<object>()
            .FirstOrDefault(candidate =>
            {
                object? candidatePlugin = GetOptionalProperty(candidate, "InstalledPlugin");
                return candidatePlugin is not null && string.Equals(
                    GetRequiredProperty(candidatePlugin, "InternalName").ToString(),
                    pluginName,
                    StringComparison.OrdinalIgnoreCase);
            });
        if (update is null)
            return Failure(pluginName, "update", dryRun, "update_not_available", $"No update is available for '{pluginName}'.");

        object? manifest = GetOptionalProperty(update, "UpdateManifest");
        string? version = GetOptionalProperty(update, "EffectiveVersion")?.ToString();
        string? repository = manifest is null ? null : GetOptionalProperty(manifest, "RepoUrl")?.ToString();
        string? sourceUrl = manifest is null ? null : GetOptionalProperty(manifest, "DownloadLinkUpdate")?.ToString();
        string previousState = GetPluginState(installedPlugin) ?? "Unknown";
        if (dryRun)
        {
            return Success(pluginName, "update", true, "dry_run", previousState, previousState, version, repository,
                $"Validated update of {pluginName} to {version}; no change was made.", sourceUrl: sourceUrl);
        }

        object? updateStatus = await InvokeTaskWithResultAsync(
            manager,
            "UpdateSinglePluginAsync",
            cancellationToken,
            update,
            true,
            false).ConfigureAwait(false);
        string status = updateStatus is null
            ? "Unknown"
            : GetRequiredProperty(updateStatus, "Status").ToString() ?? "Unknown";
        if (status is not "Success" and not "AlreadyUpToDate")
        {
            return new PluginManagementResult(
                pluginName,
                "update",
                false,
                "update_failed",
                false,
                previousState,
                GetPluginState(FindInstalledPlugin(manager, pluginName)),
                version,
                repository,
                $"Dalamud reported update status '{status}'.",
                $"Update failed for {pluginName}: {status}.");
        }

        object? updatedPlugin = FindInstalledPlugin(manager, pluginName);
        return Success(pluginName, "update", false, "completed", previousState, GetPluginState(updatedPlugin), version, repository,
            $"Updated {pluginName} to {version}.", ComputePluginHash(updatedPlugin), sourceUrl);
    }

    private static Task<PluginManagementResult> UninstallAsync(
        object manager,
        object? installedPlugin,
        string pluginName,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (installedPlugin is null)
            return Task.FromResult(Failure(pluginName, "uninstall", dryRun, "plugin_not_found", $"Plugin '{pluginName}' is not installed."));

        string previousState = GetPluginState(installedPlugin) ?? "Unknown";
        string? version = GetVersion(installedPlugin);
        string? packageSha256 = ComputePluginHash(installedPlugin);
        if (dryRun)
        {
            return Task.FromResult(Success(pluginName, "uninstall", true, "dry_run", previousState, previousState, version, null,
                $"Validated uninstall of {pluginName}; no change was made.", packageSha256));
        }

        InvokeMethod(manager, "RemovePlugin", installedPlugin);
        return Task.FromResult(Success(pluginName, "uninstall", false, "completed", previousState, "scheduled_for_deletion", version, null,
            $"Uninstall was requested for {pluginName}.", packageSha256));
    }

    private static async Task SetDefaultProfileStateAsync(
        object manager,
        object plugin,
        bool enabled,
        CancellationToken cancellationToken)
    {
        object profileManager = manager.GetType().GetField("profileManager", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(manager)
            ?? throw new MissingFieldException(manager.GetType().FullName, "profileManager");
        object defaultProfile = GetRequiredProperty(profileManager, "DefaultProfile");
        object workingPluginId = GetRequiredProperty(plugin, "EffectiveWorkingPluginId");
        string internalName = GetRequiredProperty(plugin, "InternalName").ToString()!;
        await InvokeTaskAsync(
            defaultProfile,
            "AddOrUpdateAsync",
            cancellationToken,
            workingPluginId,
            internalName,
            enabled,
            true).ConfigureAwait(false);
    }

    private static PluginManagementResult? Validate(
        string pluginName,
        string action,
        HashSet<string> allowedActions)
    {
        if (pluginName.Length == 0)
            return Failure(pluginName, action, false, "validation_failed", "plugin-name is required.");
        if (!allowedActions.Contains(action))
        {
            return Failure(
                pluginName,
                action,
                false,
                "validation_failed",
                $"action must be one of: {string.Join(", ", allowedActions.Order(StringComparer.Ordinal))}.");
        }

        return null;
    }

    private object CreateEnum(string typeName, string value) =>
        Enum.Parse(GetRequiredType(typeName), value, ignoreCase: false);

    private Type GetRequiredType(string typeName) =>
        dalamudAssembly.GetType(typeName, throwOnError: true)!;

    private static object? FindInstalledPlugin(object manager, string pluginName) =>
        FindByInternalName(GetEnumerableProperty(manager, "InstalledPlugins"), pluginName);

    private static object? FindByInternalName(IEnumerable items, string pluginName) =>
        items.Cast<object>().FirstOrDefault(item => string.Equals(
            GetRequiredProperty(item, "InternalName").ToString(),
            pluginName,
            StringComparison.OrdinalIgnoreCase));

    private static IEnumerable GetEnumerableProperty(object instance, string propertyName) =>
        GetRequiredProperty(instance, propertyName) as IEnumerable
        ?? throw new InvalidOperationException($"Property '{propertyName}' is not enumerable.");

    private static object GetRequiredProperty(object instance, string propertyName) =>
        GetOptionalProperty(instance, propertyName)
        ?? throw new MissingMemberException(instance.GetType().FullName, propertyName);

    private static object? GetOptionalProperty(object instance, string propertyName) =>
        instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(instance);

    private static string? GetVersion(object? plugin) =>
        plugin is null ? null : GetOptionalProperty(plugin, "EffectiveVersion")?.ToString();

    private static string? ComputePluginHash(object? plugin)
    {
        if (plugin is null)
            return null;

        string? path = GetOptionalProperty(plugin, "DllFile") switch
        {
            FileInfo file => file.FullName,
            string value => value,
            _ => null,
        };
        if (path is null && GetOptionalProperty(plugin, "Assembly") is Assembly assembly)
            path = assembly.Location;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? GetPluginState(object? plugin)
    {
        if (plugin is null)
            return null;

        object? state = GetOptionalProperty(plugin, "State");
        return state?.ToString() ?? (GetOptionalProperty(plugin, "IsLoaded") as bool? == true ? "Loaded" : "Unloaded");
    }

    private static async Task InvokeTaskAsync(
        object instance,
        string methodName,
        CancellationToken cancellationToken,
        params object?[] arguments)
    {
        object? result = InvokeMethod(instance, methodName, arguments);
        if (result is not Task task)
            throw new InvalidOperationException($"{instance.GetType().FullName}.{methodName} did not return a Task.");
        await task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<object?> InvokeTaskWithResultAsync(
        object instance,
        string methodName,
        CancellationToken cancellationToken,
        params object?[] arguments)
    {
        object? result = InvokeMethod(instance, methodName, arguments);
        if (result is not Task task)
            throw new InvalidOperationException($"{instance.GetType().FullName}.{methodName} did not return a Task.");
        await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        return task.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance)?.GetValue(task);
    }

    private static object? InvokeMethod(object instance, string methodName, params object?[] arguments)
    {
        MethodInfo? method = instance.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(candidate => candidate.Name == methodName && candidate.GetParameters().Length == arguments.Length);
        if (method is null)
            throw new MissingMethodException(instance.GetType().FullName, methodName);
        return method.Invoke(instance, arguments);
    }

    private static Exception Unwrap(Exception exception) =>
        exception is TargetInvocationException { InnerException: { } innerException }
            ? innerException
            : exception;

    private static PluginManagementResult Success(
        string pluginName,
        string action,
        bool dryRun,
        string status,
        string? previousState,
        string? currentState,
        string? version,
        string? repository,
        string summary,
        string? packageSha256 = null,
        string? sourceUrl = null) =>
        new(pluginName, action, true, status, dryRun, previousState, currentState, version, repository, null, summary, packageSha256, sourceUrl);

    private static PluginManagementResult Failure(
        string pluginName,
        string action,
        bool dryRun,
        string status,
        string error) =>
        new(pluginName, action, false, status, dryRun, null, null, null, null, error, error);
}
