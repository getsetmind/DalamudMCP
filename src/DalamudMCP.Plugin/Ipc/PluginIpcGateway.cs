using System.Reflection;
using Dalamud.Plugin;

namespace DalamudMCP.Plugin.Ipc;

internal sealed class PluginIpcGateway : IPluginIpcGateway
{
    private static readonly MethodInfo[] GetSubscriberMethods = typeof(IDalamudPluginInterface)
        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .Where(static method =>
            string.Equals(method.Name, "GetIpcSubscriber", StringComparison.Ordinal) &&
            method.IsGenericMethodDefinition)
        .OrderBy(static method => method.GetGenericArguments().Length)
        .ToArray();

    private readonly IDalamudPluginInterface pluginInterface;

    public PluginIpcGateway(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
    }

    public bool TryCreate(string callgate, IReadOnlyList<Type> typeArguments, out IPluginCallGateSubscriber? subscriber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callgate);
        ArgumentNullException.ThrowIfNull(typeArguments);

        MethodInfo? method = GetSubscriberMethods.FirstOrDefault(candidate => candidate.GetGenericArguments().Length == typeArguments.Count);
        if (method is null)
        {
            subscriber = null;
            return false;
        }

        object? rawSubscriber = method.MakeGenericMethod(typeArguments.ToArray()).Invoke(pluginInterface, [callgate]);
        if (rawSubscriber is null)
        {
            subscriber = null;
            return false;
        }

        subscriber = new ReflectionPluginCallGateSubscriber(rawSubscriber);
        return true;
    }
}
