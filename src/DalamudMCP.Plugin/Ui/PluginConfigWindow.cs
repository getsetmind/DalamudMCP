using System.Numerics;
using Dalamud.Bindings.ImGui;
using DalamudMCP.Application.Services;
using DalamudMCP.Application.UseCases.Settings;
using DalamudMCP.Domain.Capabilities;
using DalamudMCP.Domain.Policy;
using DalamudMCP.Plugin.Configuration;
using DalamudMCP.Plugin.Hosting;

namespace DalamudMCP.Plugin.Ui;

public sealed class PluginConfigWindow
{
    private readonly PluginCompositionRoot compositionRoot;
    private readonly PluginUiConfigurationStore configurationStore;
    private readonly PluginHostController hostController;
    private readonly UpdateExposurePolicyUseCase updateExposurePolicyUseCase;
    private ExposurePolicy? policy;
    private string? policyMessage;
    private bool isOpen;

    public PluginConfigWindow(
        PluginCompositionRoot compositionRoot,
        PluginUiConfigurationStore configurationStore,
        PluginHostController hostController)
    {
        this.compositionRoot = compositionRoot;
        this.configurationStore = configurationStore;
        this.hostController = hostController;
        updateExposurePolicyUseCase = new UpdateExposurePolicyUseCase(
            compositionRoot.SettingsRepository,
            compositionRoot.AuditLogWriter,
            new SettingsMutationGuard(compositionRoot.CapabilityRegistry));
    }

    public void Open()
    {
        isOpen = true;
    }

    public void Draw()
    {
        if (!isOpen)
        {
            return;
        }

        ImGui.SetNextWindowSize(new Vector2(720f, 0f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("DalamudMCP Settings", ref isOpen, ImGuiWindowFlags.NoCollapse))
        {
            ImGui.End();
            return;
        }

        var configuration = configurationStore.Current;
        EnsurePolicyLoaded();
        var autoLaunchHttpServerOnLoad = configuration.AutoLaunchHttpServerOnLoad;
        if (ImGui.Checkbox("Auto-launch local HTTP MCP server on plugin load", ref autoLaunchHttpServerOnLoad))
        {
            configurationStore.Update(staticConfig => staticConfig.AutoLaunchHttpServerOnLoad = autoLaunchHttpServerOnLoad);
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Bridge");
        ImGui.TextWrapped($"Pipe name: {compositionRoot.Options.PipeName}");
        ImGui.TextWrapped($"Policy file: {compositionRoot.Options.SettingsFilePath}");

        ImGui.Separator();
        ImGui.TextUnformatted("Host");
        ImGui.TextWrapped("The local MCP server can run in background HTTP mode for external MCP clients, or in a visible console for debugging.");
        ImGui.TextWrapped($"Status: {(hostController.IsRunning ? "running" : "stopped")}");
        var httpPort = configuration.HttpPort;
        if (ImGui.InputInt("HTTP port", ref httpPort))
        {
            httpPort = Math.Clamp(httpPort, 1, 65535);
            configurationStore.Update(staticConfig => staticConfig.HttpPort = httpPort);
        }

        var httpResolution = hostController.PreviewHttpLaunch(configuration.HttpPort);
        if (httpResolution is null)
        {
            ImGui.TextWrapped("HTTP host DLL could not be resolved from the current plugin output tree.");
        }
        else
        {
            ImGui.TextWrapped($"HTTP endpoint: {httpResolution.EndpointUrl}");
            ImGui.TextWrapped($"dotnet: {httpResolution.DotNetExecutable}");
            ImGui.TextWrapped($"host dll: {httpResolution.HostDllPath}");
            if (ImGui.Button("Copy HTTP Launch Command"))
            {
                ImGui.SetClipboardText(httpResolution.CommandText);
            }
        }

        if (!string.IsNullOrWhiteSpace(hostController.LastError))
        {
            ImGui.TextWrapped($"Last error: {hostController.LastError}");
        }

        if (!hostController.IsRunning)
        {
            if (ImGui.Button("Start Local HTTP Server"))
            {
                hostController.TryStartHttpServer(configuration.HttpPort);
            }

            ImGui.SameLine();
            if (ImGui.Button("Start Debug Console"))
            {
                hostController.TryStartConsole();
            }
        }
        else if (ImGui.Button("Stop Host"))
        {
            hostController.Stop();
        }

        DrawPolicySection();

        ImGui.End();
    }

    private void DrawPolicySection()
    {
        ImGui.Separator();
        ImGui.TextUnformatted("Exposure Policy");

        if (ImGui.Button("Reload Policy"))
        {
            ReloadPolicy();
        }

        ImGui.SameLine();
        if (ImGui.Button("Enable Core Action Tools"))
        {
            ApplyCoreActionPolicy();
        }

        if (!string.IsNullOrWhiteSpace(policyMessage))
        {
            ImGui.TextWrapped(policyMessage);
        }

        if (policy is null)
        {
            ImGui.TextWrapped("Policy could not be loaded.");
            return;
        }

        var observationProfileEnabled = policy.ObservationProfileEnabled;
        if (ImGui.Checkbox("Observation Profile Enabled", ref observationProfileEnabled))
        {
            SavePolicy(policy.WithProfiles(observationProfileEnabled, policy.ActionProfileEnabled));
        }

        var actionProfileEnabled = policy.ActionProfileEnabled;
        if (ImGui.Checkbox("Action Profile Enabled", ref actionProfileEnabled))
        {
            SavePolicy(policy.WithProfiles(policy.ObservationProfileEnabled, actionProfileEnabled));
        }

        if (ImGui.CollapsingHeader("Tools", ImGuiTreeNodeFlags.DefaultOpen))
        {
            foreach (var tool in GetKnownTools())
            {
                var enabled = policy.EnabledTools.Contains(tool.ToolName, StringComparer.OrdinalIgnoreCase);
                var label = $"{tool.ToolName} [{tool.Profile}]";
                if (ImGui.Checkbox(label, ref enabled))
                {
                    SavePolicy(enabled ? policy.EnableTool(tool.ToolName) : policy.DisableTool(tool.ToolName));
                }
            }
        }
    }

    private void EnsurePolicyLoaded()
    {
        if (policy is not null)
        {
            return;
        }

        ReloadPolicy();
    }

    private void ReloadPolicy()
    {
        try
        {
            policy = compositionRoot.SettingsRepository.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            policyMessage = "Policy loaded.";
        }
        catch (Exception exception)
        {
            policyMessage = $"Failed to load policy: {exception.Message}";
        }
    }

    private void ApplyCoreActionPolicy()
    {
        if (policy is null)
        {
            ReloadPolicy();
        }

        if (policy is null)
        {
            return;
        }

        var updated = policy
            .EnableTool("get_session_status")
            .EnableTool("get_player_context")
            .EnableTool("get_nearby_interactables")
            .EnableTool("target_object")
            .EnableTool("interact_with_target")
            .EnableTool("move_to_entity")
            .EnableTool("teleport_to_aetheryte")
            .EnableTool("send_addon_callback_int")
            .EnableTool("send_addon_callback_values")
            .WithProfiles(policy.ObservationProfileEnabled, actionProfileEnabled: true);
        SavePolicy(updated);
    }

    private void SavePolicy(ExposurePolicy nextPolicy)
    {
        try
        {
            updateExposurePolicyUseCase.ExecuteAsync(nextPolicy, CancellationToken.None).GetAwaiter().GetResult();
            policy = nextPolicy;
            policyMessage = "Policy saved.";
        }
        catch (Exception exception)
        {
            policyMessage = $"Failed to save policy: {exception.Message}";
        }
    }

    private IEnumerable<(string ToolName, ProfileType Profile)> GetKnownTools()
    {
        return compositionRoot.CapabilityRegistry.ToolBindings
            .Join(
                compositionRoot.CapabilityRegistry.Capabilities,
                static binding => binding.CapabilityId.Value,
                static capability => capability.Id.Value,
                static (binding, capability) => (binding.ToolName, capability.Profile))
            .OrderBy(static entry => entry.Profile)
            .ThenBy(static entry => entry.ToolName, StringComparer.OrdinalIgnoreCase);
    }
}
