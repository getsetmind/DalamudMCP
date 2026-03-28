using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Dalamud.Game.NativeWrapper;
using Dalamud.Plugin.Services;
using DalamudMCP.Framework;
using DalamudMCP.Protocol;
using FFXIVClientStructs.FFXIV.Component.GUI;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "addon.event",
    Description = "Sends a UI event to an allowlisted addon.",
    Summary = "Sends an addon event.")]
[ResultFormatter(typeof(AddonEventOperation.TextFormatter))]
[CliCommand("addon", "event")]
[McpTool("send_addon_event")]
public sealed partial class AddonEventOperation : IOperation<AddonEventOperation.Request, AddonEventResult>
{
    private readonly Func<Request, CancellationToken, ValueTask<AddonEventResult>> executor;

    [SupportedOSPlatform("windows")]
    public AddonEventOperation(
        IFramework framework,
        IClientState clientState,
        IGameGui gameGui)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(clientState);
        ArgumentNullException.ThrowIfNull(gameGui);

        executor = CreateDalamudExecutor(framework, clientState, gameGui);
    }

    internal AddonEventOperation(Func<Request, CancellationToken, ValueTask<AddonEventResult>> executor)
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public ValueTask<AddonEventResult> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(request, context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("addon.event")]
    [LegacyBridgeRequest("SendAddonEvent")]
    public sealed partial class Request
    {
        [Option("addon", Description = "Addon name to target.")]
        public string AddonName { get; init; } = string.Empty;

        [Option("event-type", Description = "Event type such as 'buttonClick'.")]
        public string EventType { get; init; } = string.Empty;

        [Option("event-param", Description = "Optional event parameter.", Required = false)]
        public int? EventParam { get; init; }

        [Option("collision-index", Description = "Optional collision index.", Required = false)]
        public int? CollisionIndex { get; init; }

        [Option("node-id", Description = "Optional node id.", Required = false)]
        public int? NodeId { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<AddonEventResult>
    {
        public string? FormatText(AddonEventResult result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return result.SummaryText;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Func<Request, CancellationToken, ValueTask<AddonEventResult>> CreateDalamudExecutor(
        IFramework framework,
        IClientState clientState,
        IGameGui gameGui)
    {
        return async (request, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            string addonName = NormalizeAddonName(request.AddonName);
            string eventTypeName = NormalizeRequiredText(request.EventType, nameof(request.EventType));

            if (framework.IsInFrameworkUpdateThread)
            {
                return SendEventCore(
                    clientState,
                    gameGui,
                    addonName,
                    eventTypeName,
                    request.EventParam,
                    request.CollisionIndex,
                    request.NodeId,
                    cancellationToken);
            }

            return await framework.RunOnFrameworkThread(
                    () => SendEventCore(
                        clientState,
                        gameGui,
                        addonName,
                        eventTypeName,
                        request.EventParam,
                        request.CollisionIndex,
                        request.NodeId,
                        cancellationToken))
                .ConfigureAwait(false);
        };
    }

    [SupportedOSPlatform("windows")]
    private static unsafe AddonEventResult SendEventCore(
        IClientState clientState,
        IGameGui gameGui,
        string addonName,
        string eventTypeName,
        int? eventParam,
        int? collisionIndex,
        int? nodeId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!clientState.IsLoggedIn)
        {
            return new AddonEventResult(
                addonName,
                eventTypeName,
                eventParam ?? -1,
                collisionIndex,
                nodeId,
                false,
                "not_logged_in",
                "Player is not logged in.");
        }

        if (!TryParseEventType(eventTypeName, out AddonEventType eventType))
        {
            return new AddonEventResult(
                addonName,
                eventTypeName,
                eventParam ?? -1,
                collisionIndex,
                nodeId,
                false,
                "invalid_event_type",
                $"'{eventTypeName}' is not a supported event type.");
        }

        if (!TryGetReadyAddon(gameGui, addonName, out AtkUnitBase* addonStruct, out string reason, out string summary))
        {
            return new AddonEventResult(
                addonName,
                eventTypeName,
                eventParam ?? -1,
                collisionIndex,
                nodeId,
                false,
                reason,
                summary);
        }

        AtkResNode* sourceNode = ResolveEventSourceNode(addonStruct, collisionIndex, nodeId, eventType, eventParam.HasValue, out string? sourceReason);
        if (sourceReason is not null)
        {
            return new AddonEventResult(
                addonName,
                ToExternalName(eventType),
                eventParam ?? -1,
                collisionIndex,
                nodeId,
                false,
                sourceReason,
                BuildEventFailureSummary(addonName, eventType, collisionIndex, nodeId, sourceReason));
        }

        if (!TryResolveEventParam(sourceNode, eventType, eventParam, out int resolvedEventParam, out string? eventReason))
        {
            return new AddonEventResult(
                addonName,
                ToExternalName(eventType),
                eventParam ?? -1,
                collisionIndex,
                nodeId,
                false,
                eventReason,
                BuildEventFailureSummary(addonName, eventType, collisionIndex, nodeId, eventReason));
        }

        if (sourceNode == null)
        {
            return new AddonEventResult(
                addonName,
                ToExternalName(eventType),
                resolvedEventParam,
                collisionIndex,
                nodeId,
                false,
                "event_source_unavailable",
                BuildEventFailureSummary(addonName, eventType, collisionIndex, nodeId, "event_source_unavailable"));
        }

        if (eventType is AddonEventType.SystemMouseClick)
        {
            if (!TryPerformSystemMouseClick(addonStruct, sourceNode, out string? systemMouseReason, out string clickSummary))
            {
                return new AddonEventResult(
                    addonName,
                    ToExternalName(eventType),
                    resolvedEventParam,
                    collisionIndex,
                    nodeId,
                    false,
                    systemMouseReason,
                    BuildEventFailureSummary(addonName, eventType, collisionIndex, nodeId, systemMouseReason));
            }

            return new AddonEventResult(
                addonName,
                ToExternalName(eventType),
                resolvedEventParam,
                collisionIndex,
                nodeId,
                true,
                null,
                clickSummary);
        }

        AtkEventType nativeEventType = ToNativeEventType(eventType);
        AtkEventData eventData = CreateEventData(sourceNode, nativeEventType, resolvedEventParam);
        bool dispatchHandled = TryDispatchEvent(addonStruct, sourceNode, nativeEventType, resolvedEventParam, ref eventData);
        if (!dispatchHandled)
        {
            AtkEvent atkEvent = CreateNativeEvent(sourceNode, addonStruct, nativeEventType, resolvedEventParam);
            addonStruct->ReceiveEvent(nativeEventType, resolvedEventParam, &atkEvent, &eventData);
        }

        return new AddonEventResult(
            addonName,
            ToExternalName(eventType),
            resolvedEventParam,
            collisionIndex,
            nodeId,
            true,
            null,
            BuildEventSuccessSummary(
                addonName,
                eventType,
                resolvedEventParam,
                collisionIndex,
                nodeId,
                eventParam.HasValue,
                !collisionIndex.HasValue && !nodeId.HasValue && sourceNode == addonStruct->FocusNode,
                dispatchHandled));
    }

    [SupportedOSPlatform("windows")]
    private static unsafe bool TryGetReadyAddon(
        IGameGui gameGui,
        string addonName,
        out AtkUnitBase* addonStruct,
        out string reason,
        out string summary)
    {
        addonStruct = null;
        AtkUnitBasePtr addon = gameGui.GetAddonByName(addonName, 1);
        if (addon.IsNull || !addon.IsReady)
        {
            reason = "addon_not_ready";
            summary = $"{addonName} is not ready.";
            return false;
        }

        addonStruct = gameGui.GetAddonByName<AtkUnitBase>(addonName, 1);
        if (addonStruct is null)
        {
            reason = "addon_struct_unavailable";
            summary = $"{addonName} does not expose a native addon pointer.";
            return false;
        }

        reason = string.Empty;
        summary = string.Empty;
        return true;
    }

    [SupportedOSPlatform("windows")]
    private static unsafe AtkResNode* ResolveEventSourceNode(
        AtkUnitBase* addonStruct,
        int? collisionIndex,
        int? nodeId,
        AddonEventType eventType,
        bool allowMissingFocus,
        out string? reason)
    {
        reason = null;
        if (nodeId.HasValue)
        {
            AtkResNode* node = addonStruct->GetNodeById((uint)nodeId.Value);
            if (node == null)
            {
                reason = "node_id_not_found";
                return null;
            }

            return node;
        }

        if (collisionIndex.HasValue)
        {
            if (collisionIndex.Value < 0 || collisionIndex.Value >= addonStruct->CollisionNodeListCount)
            {
                reason = "collision_index_out_of_range";
                return null;
            }

            AtkResNode* collisionNode = addonStruct->CollisionNodeList[(uint)collisionIndex.Value];
            if (collisionNode == null)
            {
                reason = "collision_node_missing";
                return null;
            }

            if (TryResolveAdjacentButtonEventNode(addonStruct, collisionIndex.Value, collisionNode, eventType, out AtkResNode* adjacentNode))
                return adjacentNode;

            return collisionNode;
        }

        if (addonStruct->FocusNode != null)
            return addonStruct->FocusNode;

        if (!allowMissingFocus)
            reason = "focus_node_unavailable";

        return null;
    }

    [SupportedOSPlatform("windows")]
    private static unsafe bool TryResolveAdjacentButtonEventNode(
        AtkUnitBase* addonStruct,
        int collisionIndex,
        AtkResNode* requestedNode,
        AddonEventType eventType,
        out AtkResNode* adjacentNode)
    {
        adjacentNode = null;
        if (eventType is not (AddonEventType.ButtonClick or AddonEventType.ButtonPress))
            return false;

        AtkEventType nativeEventType = ToNativeEventType(eventType);
        if (requestedNode->IsEventRegistered(nativeEventType))
            return false;

        foreach (int candidateIndex in new[] { collisionIndex - 1, collisionIndex + 1 })
        {
            if (candidateIndex < 0 || candidateIndex >= addonStruct->CollisionNodeListCount)
                continue;

            AtkResNode* candidateNode = addonStruct->CollisionNodeList[(uint)candidateIndex];
            if (candidateNode == null || !candidateNode->IsEventRegistered(nativeEventType))
                continue;

            if (!HaveEquivalentBounds(requestedNode, candidateNode))
                continue;

            adjacentNode = candidateNode;
            return true;
        }

        return false;
    }

    [SupportedOSPlatform("windows")]
    private static unsafe bool HaveEquivalentBounds(AtkResNode* left, AtkResNode* right)
    {
        return MathF.Abs(left->ScreenX - right->ScreenX) < 0.5f
               && MathF.Abs(left->ScreenY - right->ScreenY) < 0.5f
               && left->Width == right->Width
               && left->Height == right->Height;
    }

    [SupportedOSPlatform("windows")]
    private static unsafe bool TryResolveEventParam(
        AtkResNode* sourceNode,
        AddonEventType eventType,
        int? explicitEventParam,
        out int resolvedEventParam,
        out string? reason)
    {
        if (explicitEventParam.HasValue)
        {
            resolvedEventParam = explicitEventParam.Value;
            reason = null;
            return true;
        }

        if (sourceNode == null)
        {
            resolvedEventParam = default;
            reason = "event_source_unavailable";
            return false;
        }

        AtkEventType nativeEventType = ToNativeEventType(eventType);
        if (!sourceNode->IsEventRegistered(nativeEventType))
        {
            resolvedEventParam = default;
            reason = "event_not_registered";
            return false;
        }

        resolvedEventParam = (int)sourceNode->GetEventParam(nativeEventType);
        reason = null;
        return true;
    }

    [SupportedOSPlatform("windows")]
    private static unsafe AtkEvent CreateNativeEvent(
        AtkResNode* sourceNode,
        AtkUnitBase* addonStruct,
        AtkEventType eventType,
        int eventParam)
    {
        return new AtkEvent
        {
            Node = sourceNode,
            Target = (AtkEventTarget*)sourceNode,
            Listener = (AtkEventListener*)addonStruct,
            Param = (uint)eventParam,
            NextEvent = null,
            State = new AtkEventState
            {
                EventType = eventType,
                ReturnFlags = 0,
                StateFlags = AtkEventStateFlags.None
            }
        };
    }

    [SupportedOSPlatform("windows")]
    private static unsafe AtkEventData CreateEventData(
        AtkResNode* sourceNode,
        AtkEventType eventType,
        int eventParam)
    {
        return eventType switch
        {
            AtkEventType.InputNavigation => new AtkEventData
            {
                InputData = new AtkEventData.AtkInputData
                {
                    InputId = eventParam,
                    State = AtkEventData.AtkInputData.InputState.Repeat,
                    Modifier = AtkEventData.AtkInputData.ModifierFlag.None
                }
            },
            AtkEventType.InputReceived => new AtkEventData
            {
                InputData = new AtkEventData.AtkInputData
                {
                    InputId = eventParam,
                    State = AtkEventData.AtkInputData.InputState.Down,
                    Modifier = AtkEventData.AtkInputData.ModifierFlag.None
                }
            },
            _ => new AtkEventData
            {
                MouseData = CreateMouseData(sourceNode)
            }
        };
    }

    [SupportedOSPlatform("windows")]
    private static unsafe AtkEventData.AtkMouseData CreateMouseData(AtkResNode* sourceNode)
    {
        return new AtkEventData.AtkMouseData
        {
            PosX = GetNodeCenterCoordinate(sourceNode->ScreenX, sourceNode->Width),
            PosY = GetNodeCenterCoordinate(sourceNode->ScreenY, sourceNode->Height),
            WheelDirection = 0,
            ButtonId = 1,
            Modifier = AtkEventData.AtkMouseData.ModifierFlag.None
        };
    }

    [SupportedOSPlatform("windows")]
    private static unsafe bool TryDispatchViaSourceNode(
        AtkResNode* sourceNode,
        AtkEventType eventType,
        ref AtkEventData eventData)
    {
        AtkEventDispatcher.Event dispatchEvent = new()
        {
            State = new AtkEventState
            {
                EventType = eventType,
                ReturnFlags = 0,
                StateFlags = AtkEventStateFlags.None
            },
            ReturnFlags = 0,
            EventData = eventData
        };

        return sourceNode->DispatchEvent(&dispatchEvent);
    }

    [SupportedOSPlatform("windows")]
    private static unsafe bool TryDispatchEvent(
        AtkUnitBase* addonStruct,
        AtkResNode* sourceNode,
        AtkEventType eventType,
        int eventParam,
        ref AtkEventData eventData)
    {
        if (eventType is AtkEventType.MouseClick)
            return TryDispatchMouseClick(addonStruct, sourceNode, eventParam, ref eventData);

        if (eventType is AtkEventType.MouseDown or AtkEventType.MouseUp)
            PrepareMouseInteraction(addonStruct, sourceNode, eventParam, ref eventData);

        return TryDispatchViaSourceNode(sourceNode, eventType, ref eventData);
    }

    [SupportedOSPlatform("windows")]
    private static unsafe bool TryDispatchMouseClick(
        AtkUnitBase* addonStruct,
        AtkResNode* sourceNode,
        int eventParam,
        ref AtkEventData eventData)
    {
        PrepareMouseInteraction(addonStruct, sourceNode, eventParam, ref eventData);

        AtkEventData mouseDownData = eventData;
        AtkEventData mouseUpData = eventData;
        AtkEventData clickData = eventData;

        bool handled = TryDispatchViaSourceNode(sourceNode, AtkEventType.MouseDown, ref mouseDownData);
        handled |= TryDispatchViaSourceNode(sourceNode, AtkEventType.MouseUp, ref mouseUpData);
        handled |= TryDispatchViaSourceNode(sourceNode, AtkEventType.MouseClick, ref clickData);
        eventData = clickData;
        return handled;
    }

    [SupportedOSPlatform("windows")]
    private static unsafe void PrepareMouseInteraction(
        AtkUnitBase* addonStruct,
        AtkResNode* sourceNode,
        int eventParam,
        ref AtkEventData eventData)
    {
        AtkCollisionNode* collisionNode = sourceNode->GetAsAtkCollisionNode();
        if (collisionNode == null)
            return;

        AtkStage* stage = AtkStage.Instance();
        AtkInputManager* inputManager = stage == null ? null : stage->AtkInputManager;
        if (inputManager == null)
            return;

        inputManager->SetFocus(sourceNode, addonStruct, eventParam);
        addonStruct->SetFocusNode(sourceNode, true, (uint)eventParam);
        inputManager->FocusedNode = sourceNode;
        addonStruct->CursorTarget = sourceNode;
        addonStruct->HandleCursorTypeChange();
        addonStruct->OnMouseOver();

        AtkCollisionNode** savedCollisionNodes = (AtkCollisionNode**)((byte*)inputManager + 0x08);
        savedCollisionNodes[0] = collisionNode;

        AtkInputManager.SavedMouseClick* savedMouseClicks =
            (AtkInputManager.SavedMouseClick*)((byte*)inputManager + 0x40);
        savedMouseClicks[0] = new AtkInputManager.SavedMouseClick
        {
            Timestamp = Environment.TickCount,
            X = eventData.MouseData.PosX,
            Y = eventData.MouseData.PosY
        };
    }

    private static short GetNodeCenterCoordinate(float screenCoordinate, ushort size)
    {
        float center = screenCoordinate + size / 2f;
        if (center <= short.MinValue)
            return short.MinValue;

        if (center >= short.MaxValue)
            return short.MaxValue;

        return (short)center;
    }

    private static bool TryParseEventType(string value, out AddonEventType eventType)
    {
        switch (value.Trim())
        {
            case "mouseDown":
                eventType = AddonEventType.MouseDown;
                return true;
            case "mouseUp":
                eventType = AddonEventType.MouseUp;
                return true;
            case "mouseClick":
                eventType = AddonEventType.MouseClick;
                return true;
            case "systemMouseClick":
                eventType = AddonEventType.SystemMouseClick;
                return true;
            case "inputReceived":
                eventType = AddonEventType.InputReceived;
                return true;
            case "inputNavigation":
                eventType = AddonEventType.InputNavigation;
                return true;
            case "buttonPress":
                eventType = AddonEventType.ButtonPress;
                return true;
            case "buttonClick":
                eventType = AddonEventType.ButtonClick;
                return true;
            case "listButtonPress":
                eventType = AddonEventType.ListButtonPress;
                return true;
            case "listItemClick":
                eventType = AddonEventType.ListItemClick;
                return true;
            default:
                eventType = default;
                return false;
        }
    }

    private static string NormalizeAddonName(string addonName)
    {
        return string.IsNullOrWhiteSpace(addonName)
            ? throw new ArgumentException("addon is required.", nameof(addonName))
            : addonName.Trim();
    }

    private static string NormalizeRequiredText(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{parameterName} is required.", parameterName)
            : value.Trim();
    }

    private static string ToExternalName(AddonEventType eventType)
    {
        return eventType switch
        {
            AddonEventType.MouseDown => "mouseDown",
            AddonEventType.MouseUp => "mouseUp",
            AddonEventType.MouseClick => "mouseClick",
            AddonEventType.SystemMouseClick => "systemMouseClick",
            AddonEventType.InputReceived => "inputReceived",
            AddonEventType.InputNavigation => "inputNavigation",
            AddonEventType.ButtonPress => "buttonPress",
            AddonEventType.ButtonClick => "buttonClick",
            AddonEventType.ListButtonPress => "listButtonPress",
            AddonEventType.ListItemClick => "listItemClick",
            _ => eventType.ToString()
        };
    }

    private static AtkEventType ToNativeEventType(AddonEventType eventType)
    {
        return eventType switch
        {
            AddonEventType.MouseDown => AtkEventType.MouseDown,
            AddonEventType.MouseUp => AtkEventType.MouseUp,
            AddonEventType.MouseClick => AtkEventType.MouseClick,
            AddonEventType.SystemMouseClick => AtkEventType.MouseClick,
            AddonEventType.InputReceived => AtkEventType.InputReceived,
            AddonEventType.InputNavigation => AtkEventType.InputNavigation,
            AddonEventType.ButtonPress => AtkEventType.ButtonPress,
            AddonEventType.ButtonClick => AtkEventType.ButtonClick,
            AddonEventType.ListButtonPress => AtkEventType.ListButtonPress,
            AddonEventType.ListItemClick => AtkEventType.ListItemClick,
            _ => AtkEventType.MouseClick
        };
    }

    private static string BuildEventSuccessSummary(
        string addonName,
        AddonEventType eventType,
        int eventParam,
        int? collisionIndex,
        int? nodeId,
        bool usedExplicitParam,
        bool usedFocusNode,
        bool dispatchHandled)
    {
        string suffix = collisionIndex.HasValue
            ? $" via collision[{collisionIndex.Value}]"
            : nodeId.HasValue
                ? $" via nodeId[{nodeId.Value}]"
                : usedFocusNode
                    ? " via focus"
                    : usedExplicitParam
                        ? " via explicit param"
                        : string.Empty;
        string dispatchText = dispatchHandled
            ? "node dispatch handled it"
            : "node dispatch was unhandled and addon receive fallback was invoked";
        return $"Dispatched {ToExternalName(eventType)} event {eventParam} to {addonName}{suffix}; {dispatchText}.";
    }

    private static string BuildEventFailureSummary(
        string addonName,
        AddonEventType eventType,
        int? collisionIndex,
        int? nodeId,
        string? reason)
    {
        string targetText = collisionIndex.HasValue
            ? $"collision[{collisionIndex.Value}]"
            : nodeId.HasValue
                ? $"nodeId[{nodeId.Value}]"
                : "focus";
        return $"{addonName} could not dispatch {ToExternalName(eventType)} for {targetText} ({reason ?? "unknown"}).";
    }

    [SupportedOSPlatform("windows")]
    private static unsafe bool TryPerformSystemMouseClick(
        AtkUnitBase* addonStruct,
        AtkResNode* sourceNode,
        out string? reason,
        out string summaryText)
    {
        reason = null;
        summaryText = string.Empty;

        IntPtr windowHandle = TryGetGameWindowHandle();
        if (windowHandle == nint.Zero)
        {
            reason = "game_window_unavailable";
            return false;
        }

        NativePoint clientPoint = CreateClientCenterPoint(
            sourceNode->ScreenX,
            sourceNode->Width,
            sourceNode->ScreenY,
            sourceNode->Height);
        if (!TryPerformClick(windowHandle, clientPoint, out NativePoint screenPoint, out string? clickReason))
        {
            reason = clickReason;
            return false;
        }

        summaryText =
            $"Performed systemMouseClick for {addonStruct->NameString} at client ({clientPoint.X}, {clientPoint.Y}) / screen ({screenPoint.X}, {screenPoint.Y}).";
        return true;
    }

    private static nint TryGetGameWindowHandle()
    {
        IntPtr mainWindowHandle = Process.GetCurrentProcess().MainWindowHandle;
        if (mainWindowHandle != nint.Zero)
            return mainWindowHandle;

        return GetForegroundWindow();
    }

    private static NativePoint CreateClientCenterPoint(float screenX, ushort width, float screenY, ushort height)
    {
        return new NativePoint(
            (int)MathF.Round(screenX + width / 2f),
            (int)MathF.Round(screenY + height / 2f));
    }

    private static bool TryPerformClick(
        nint windowHandle,
        NativePoint clientPoint,
        out NativePoint screenPoint,
        out string? reason)
    {
        const uint InputTypeMouse = 0;
        const uint MouseEventLeftDown = 0x0002;
        const uint MouseEventLeftUp = 0x0004;

        reason = null;
        screenPoint = default;
        if (windowHandle == nint.Zero)
        {
            reason = "game_window_unavailable";
            return false;
        }

        screenPoint = clientPoint;
        if (!ClientToScreen(windowHandle, ref screenPoint))
        {
            reason = "client_to_screen_failed";
            return false;
        }

        bool hasOriginalCursor = GetCursorPos(out NativePoint originalCursor);
        try
        {
            if (!TryActivateWindow(windowHandle))
            {
                reason = "activate_window_failed";
                return false;
            }

            if (!SetCursorPos(screenPoint.X, screenPoint.Y))
            {
                reason = "set_cursor_failed";
                return false;
            }

            Thread.Sleep(50);

            NativeInput[] inputs =
            [
                CreateMouseInput(InputTypeMouse, MouseEventLeftDown),
                CreateMouseInput(InputTypeMouse, MouseEventLeftUp)
            ];
            uint sentCount = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeInput>());
            if (sentCount != (uint)inputs.Length)
            {
                reason = "send_input_failed";
                return false;
            }

            Thread.Sleep(100);
            return true;
        }
        finally
        {
            if (hasOriginalCursor)
                _ = SetCursorPos(originalCursor.X, originalCursor.Y);
        }
    }

    private static NativeInput CreateMouseInput(uint inputType, uint flags)
    {
        return new NativeInput
        {
            Type = inputType,
            Data = new NativeInputData
            {
                MouseInput = new NativeMouseInput
                {
                    DwFlags = flags
                }
            }
        };
    }

    private static bool TryActivateWindow(nint windowHandle)
    {
        const int ShowWindowRestore = 9;

        IntPtr foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == windowHandle)
            return true;

        uint currentThreadId = GetCurrentThreadId();
        uint foregroundThreadId = foregroundWindow == nint.Zero
            ? 0u
            : GetWindowThreadProcessId(foregroundWindow, out _);
        uint windowThreadId = GetWindowThreadProcessId(windowHandle, out _);

        bool attachedToForeground = false;
        bool attachedToWindow = false;
        try
        {
            if (foregroundThreadId != 0 && foregroundThreadId != currentThreadId)
                attachedToForeground = AttachThreadInput(currentThreadId, foregroundThreadId, true);

            if (windowThreadId != 0 && windowThreadId != currentThreadId)
                attachedToWindow = AttachThreadInput(currentThreadId, windowThreadId, true);

            _ = ShowWindow(windowHandle, ShowWindowRestore);
            _ = BringWindowToTop(windowHandle);
            _ = SetActiveWindow(windowHandle);
            _ = SetFocus(windowHandle);
            _ = SetForegroundWindow(windowHandle);

            Thread.Sleep(20);
            return GetForegroundWindow() == windowHandle;
        }
        finally
        {
            if (attachedToWindow)
                _ = AttachThreadInput(currentThreadId, windowThreadId, false);

            if (attachedToForeground)
                _ = AttachThreadInput(currentThreadId, foregroundThreadId, false);
        }
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(nint hWnd, ref NativePoint point);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetActiveWindow(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetFocus(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, [In] NativeInput[] inputs, int inputSize);
}

public enum AddonEventType
{
    MouseDown,
    MouseUp,
    MouseClick,
    SystemMouseClick,
    InputReceived,
    InputNavigation,
    ButtonPress,
    ButtonClick,
    ListButtonPress,
    ListItemClick
}

[MemoryPackable]
public sealed partial record AddonEventResult(
    string AddonName,
    string EventType,
    int EventParam,
    int? CollisionIndex,
    int? NodeId,
    bool Succeeded,
    string? Reason,
    string SummaryText);

[StructLayout(LayoutKind.Sequential)]
internal struct NativePoint
{
    public int X;
    public int Y;

    public NativePoint(int x, int y)
    {
        X = x;
        Y = y;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeInput
{
    public uint Type;
    public NativeInputData Data;
}

[StructLayout(LayoutKind.Explicit)]
internal struct NativeInputData
{
    [FieldOffset(0)] public NativeMouseInput MouseInput;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeMouseInput
{
    public int Dx;
    public int Dy;
    public uint MouseData;
    public uint DwFlags;
    public uint Time;
    public nint DwExtraInfo;
}
