using System.Numerics;
using Dalamud.Bindings.ImGui;
using DalamudMCP.Plugin.Configuration;
using DalamudMCP.Plugin.Hosting;

namespace DalamudMCP.Plugin.Ui;

public sealed class PluginConfigWindow
{
    private readonly PluginRuntimeOptions runtimeOptions;
    private readonly PluginUiConfigurationStore configurationStore;
    private readonly PluginHostController hostController;
    private bool isOpen;

    public PluginConfigWindow(
        PluginRuntimeOptions runtimeOptions,
        PluginUiConfigurationStore configurationStore,
        PluginHostController hostController)
    {
        this.runtimeOptions = runtimeOptions;
        this.configurationStore = configurationStore;
        this.hostController = hostController;
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
        var autoLaunchHttpServerOnLoad = configuration.AutoLaunchHttpServerOnLoad;
        if (ImGui.Checkbox("Auto-launch local HTTP MCP server on plugin load", ref autoLaunchHttpServerOnLoad))
        {
            configurationStore.Update(staticConfig => staticConfig.AutoLaunchHttpServerOnLoad = autoLaunchHttpServerOnLoad);
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Bridge");
        ImGui.TextWrapped($"Pipe name: {runtimeOptions.PipeName}");
        ImGui.TextWrapped($"Policy file: {runtimeOptions.SettingsFilePath}");

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

        ImGui.End();
    }
}
