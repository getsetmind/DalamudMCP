using Manifold;
using Manifold.Generated;
using Microsoft.Extensions.DependencyInjection;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class GeneratedOperationInvokerTests
{
    [Fact]
    public async Task GeneratedOperationInvoker_invokes_player_context_instance_operation()
    {
        PlayerContextSnapshot expected = new(
            "Test Adventurer",
            "ExampleWorld",
            "Dancer",
            100,
            "Sample Plaza",
            new PlayerPosition(1.0, 2.0, 3.0));
        ServiceCollection services = new();
        services.AddSingleton(new PlayerContextOperation(_ => ValueTask.FromResult(expected)));
        services.AddSingleton<PlayerContextOperation.TextFormatter>();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        GeneratedOperationInvoker invoker = new();
        bool invoked = invoker.TryInvoke(
            "player.context",
            new PlayerContextOperation.Request(),
            serviceProvider,
            InvocationSurface.Protocol,
            TestContext.Current.CancellationToken,
            out ValueTask<OperationInvocationResult> invocation);

        Assert.True(invoked);

        OperationInvocationResult result = await invocation;
        Assert.Equal(typeof(PlayerContextSnapshot), result.ResultType);
        Assert.Equal(expected, Assert.IsType<PlayerContextSnapshot>(result.Result));
    }

    [Fact]
    public async Task GeneratedOperationInvoker_creates_empty_request_when_request_is_null()
    {
        SessionStatusSnapshot expected = new(
            SessionRuntimeState.Ready,
            "ready",
            [new SessionReaderStatus("player.context", true, "ready")]);
        ServiceCollection services = new();
        services.AddSingleton(new SessionStatusOperation(_ => ValueTask.FromResult(expected)));
        services.AddSingleton<SessionStatusOperation.TextFormatter>();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        GeneratedOperationInvoker invoker = new();
        bool invoked = invoker.TryInvoke(
            "session.status",
            null,
            serviceProvider,
            InvocationSurface.Protocol,
            TestContext.Current.CancellationToken,
            out ValueTask<OperationInvocationResult> invocation);

        Assert.True(invoked);

        OperationInvocationResult result = await invocation;
        Assert.Equal(typeof(SessionStatusSnapshot), result.ResultType);
        Assert.Equal(expected, Assert.IsType<SessionStatusSnapshot>(result.Result));
    }

    [Fact]
    public async Task GeneratedOperationInvoker_invokes_duty_context_instance_operation()
    {
        DutyContextSnapshot expected = new(
            777,
            "Territory#777",
            "duty",
            true,
            false,
            "Territory#777 is active.");
        ServiceCollection services = new();
        services.AddSingleton(new DutyContextOperation(_ => ValueTask.FromResult(expected)));
        services.AddSingleton<DutyContextOperation.TextFormatter>();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        GeneratedOperationInvoker invoker = new();
        bool invoked = invoker.TryInvoke(
            "duty.context",
            new DutyContextOperation.Request(),
            serviceProvider,
            InvocationSurface.Protocol,
            TestContext.Current.CancellationToken,
            out ValueTask<OperationInvocationResult> invocation);

        Assert.True(invoked);

        OperationInvocationResult result = await invocation;
        Assert.Equal(typeof(DutyContextSnapshot), result.ResultType);
        Assert.Equal(expected, Assert.IsType<DutyContextSnapshot>(result.Result));
    }

    [Fact]
    public async Task GeneratedOperationInvoker_invokes_inventory_summary_instance_operation()
    {
        InventorySummarySnapshot expected = new(
            DateTimeOffset.UtcNow,
            123456,
            87,
            140,
            new InventoryCategoryBreakdown(87, 13, 145, 5, 8),
            "87/140 main inventory slots occupied; 145 armory items, 13 equipped items, 5 currency entries, and 8 crystal stacks tracked (123456 gil tracked).");
        ServiceCollection services = new();
        services.AddSingleton(new InventorySummaryOperation(_ => ValueTask.FromResult(expected)));
        services.AddSingleton<InventorySummaryOperation.TextFormatter>();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        GeneratedOperationInvoker invoker = new();
        bool invoked = invoker.TryInvoke(
            "inventory.summary",
            new InventorySummaryOperation.Request(),
            serviceProvider,
            InvocationSurface.Protocol,
            TestContext.Current.CancellationToken,
            out ValueTask<OperationInvocationResult> invocation);

        Assert.True(invoked);

        OperationInvocationResult result = await invocation;
        Assert.Equal(typeof(InventorySummarySnapshot), result.ResultType);
        Assert.Equal(expected, Assert.IsType<InventorySummarySnapshot>(result.Result));
    }

    [Fact]
    public async Task GeneratedOperationInvoker_invokes_nearby_interactables_instance_operation()
    {
        NearbyInteractablesSnapshot expected = new(
            DateTimeOffset.UtcNow,
            8d,
            [
                new NearbyInteractable("0x123", "Summoning Bell", "EventObj", true, 3.5, 1.2, new NearbyInteractablePosition(1, 2, 3))
            ],
            "1 interactable objects within 8 yalms.");
        ServiceCollection services = new();
        services.AddSingleton(new NearbyInteractablesOperation((request, _) =>
            ValueTask.FromResult(expected)));
        services.AddSingleton<NearbyInteractablesOperation.TextFormatter>();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        GeneratedOperationInvoker invoker = new();
        bool invoked = invoker.TryInvoke(
            "nearby.interactables",
            new NearbyInteractablesOperation.Request { MaxDistance = 8d },
            serviceProvider,
            InvocationSurface.Protocol,
            TestContext.Current.CancellationToken,
            out ValueTask<OperationInvocationResult> invocation);

        Assert.True(invoked);

        OperationInvocationResult result = await invocation;
        Assert.Equal(typeof(NearbyInteractablesSnapshot), result.ResultType);
        Assert.Equal(expected, Assert.IsType<NearbyInteractablesSnapshot>(result.Result));
    }

    [Fact]
    public async Task GeneratedOperationInvoker_invokes_fate_context_instance_operation()
    {
        FateContextSnapshot expected = new(
            DateTimeOffset.UtcNow,
            144,
            120d,
            [
                new FateSnapshot(12, "Test Fate", "Running", 100, 100, 50, 600, false, 11.2, new FatePosition(1, 2, 3), "Defeat foes", null)
            ],
            "1 FATEs within 120 yalms.");
        ServiceCollection services = new();
        services.AddSingleton(new FateContextOperation((request, _) =>
            ValueTask.FromResult(expected)));
        services.AddSingleton<FateContextOperation.TextFormatter>();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        GeneratedOperationInvoker invoker = new();
        bool invoked = invoker.TryInvoke(
            "fate.context",
            new FateContextOperation.Request { MaxDistance = 120d },
            serviceProvider,
            InvocationSurface.Protocol,
            TestContext.Current.CancellationToken,
            out ValueTask<OperationInvocationResult> invocation);

        Assert.True(invoked);

        OperationInvocationResult result = await invocation;
        Assert.Equal(typeof(FateContextSnapshot), result.ResultType);
        Assert.Equal(expected, Assert.IsType<FateContextSnapshot>(result.Result));
    }

    [Fact]
    public async Task GeneratedOperationInvoker_invokes_quest_status_instance_operation()
    {
        QuestStatusSnapshot expected = new(
            DateTimeOffset.UtcNow,
            "questId:42",
            [
                new QuestStatusEntrySnapshot(42, "Into the Light", true, false, 3, 100, "Into the Light (42) is accepted at sequence 3.")
            ],
            "1 quest entries matched 'questId:42'.");
        ServiceCollection services = new();
        services.AddSingleton(new QuestStatusOperation((request, _) =>
            ValueTask.FromResult(expected)));
        services.AddSingleton<QuestStatusOperation.TextFormatter>();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        GeneratedOperationInvoker invoker = new();
        bool invoked = invoker.TryInvoke(
            "quest.status",
            new QuestStatusOperation.Request { QuestId = 42 },
            serviceProvider,
            InvocationSurface.Protocol,
            TestContext.Current.CancellationToken,
            out ValueTask<OperationInvocationResult> invocation);

        Assert.True(invoked);

        OperationInvocationResult result = await invocation;
        Assert.Equal(typeof(QuestStatusSnapshot), result.ResultType);
        Assert.Equal(expected, Assert.IsType<QuestStatusSnapshot>(result.Result));
    }

    [Fact]
    public async Task GeneratedOperationInvoker_invokes_available_quests_instance_operation()
    {
        AvailableQuestsSnapshot expected = new(
            DateTimeOffset.UtcNow,
            132,
            null,
            [
                new AvailableQuest(
                    999,
                    "Weather Report Wanted",
                    1,
                    new AvailableQuestMarker(
                        1,
                        12345,
                        123,
                        132,
                        0.5,
                        new AvailableQuestPosition(1, 2, 3)),
                    "Weather Report Wanted (999) is available in Territory#132 (level 1).")
            ],
            "1 visible unaccepted quests found in Territory#132.");
        ServiceCollection services = new();
        services.AddSingleton(new AvailableQuestsOperation((request, _) =>
            ValueTask.FromResult(expected)));
        services.AddSingleton<AvailableQuestsOperation.TextFormatter>();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        GeneratedOperationInvoker invoker = new();
        bool invoked = invoker.TryInvoke(
            "quest.available",
            new AvailableQuestsOperation.Request { MaxResults = 4 },
            serviceProvider,
            InvocationSurface.Protocol,
            TestContext.Current.CancellationToken,
            out ValueTask<OperationInvocationResult> invocation);

        Assert.True(invoked);

        OperationInvocationResult result = await invocation;
        Assert.Equal(typeof(AvailableQuestsSnapshot), result.ResultType);
        Assert.Equal(expected, Assert.IsType<AvailableQuestsSnapshot>(result.Result));
    }

    [Fact]
    public async Task GeneratedOperationInvoker_invokes_current_quest_objective_instance_operation()
    {
        CurrentQuestObjectiveSnapshot expected = new(
            DateTimeOffset.UtcNow,
            132,
            999,
            "Weather Report Wanted",
            "normal",
            1,
            [
                new CurrentQuestObjectiveVisibleMarker(
                    999,
                    12345,
                    123,
                    132,
                    456,
                    789,
                    1,
                    0.5,
                    new CurrentQuestObjectivePosition(1, 2, 3),
                    "Quest Marker")
            ],
            [
                new CurrentQuestObjectiveLinkMarker(
                    "map",
                    999,
                    "Talk to the weather forecaster.",
                    1,
                    71234,
                    123,
                    123,
                    123)
            ],
            "Weather Report Wanted (999) is tracked at sequence 1; 1 visible objective markers found in Territory#132.");
        ServiceCollection services = new();
        services.AddSingleton(new CurrentQuestObjectiveOperation(_ => ValueTask.FromResult(expected)));
        services.AddSingleton<CurrentQuestObjectiveOperation.TextFormatter>();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        GeneratedOperationInvoker invoker = new();
        bool invoked = invoker.TryInvoke(
            "quest.current-objective",
            new CurrentQuestObjectiveOperation.Request(),
            serviceProvider,
            InvocationSurface.Protocol,
            TestContext.Current.CancellationToken,
            out ValueTask<OperationInvocationResult> invocation);

        Assert.True(invoked);

        OperationInvocationResult result = await invocation;
        Assert.Equal(typeof(CurrentQuestObjectiveSnapshot), result.ResultType);
        Assert.Equal(expected, Assert.IsType<CurrentQuestObjectiveSnapshot>(result.Result));
    }

    [Fact]
    public async Task GeneratedOperationInvoker_invokes_addon_list_instance_operation()
    {
        AddonSummary[] expected =
        [
            new AddonSummary("Inventory", true, true, DateTimeOffset.UtcNow, "Inventory is open and visible.")
        ];
        ServiceCollection services = new();
        services.AddSingleton(new AddonListOperation(_ => ValueTask.FromResult(expected)));
        services.AddSingleton<AddonListOperation.TextFormatter>();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        GeneratedOperationInvoker invoker = new();
        bool invoked = invoker.TryInvoke(
            "addon.list",
            new AddonListOperation.Request(),
            serviceProvider,
            InvocationSurface.Protocol,
            TestContext.Current.CancellationToken,
            out ValueTask<OperationInvocationResult> invocation);

        Assert.True(invoked);

        OperationInvocationResult result = await invocation;
        Assert.Equal(typeof(AddonSummary[]), result.ResultType);
        Assert.Equal(expected, Assert.IsType<AddonSummary[]>(result.Result));
    }

    [Fact]
    public async Task GeneratedOperationInvoker_invokes_addon_strings_instance_operation()
    {
        AddonStringsSnapshot expected = new(
            "Inventory",
            DateTimeOffset.UtcNow,
            [new AddonStringEntry(0, "hello", "hello")]);
        ServiceCollection services = new();
        services.AddSingleton(new AddonStringsOperation((request, _) =>
            ValueTask.FromResult(expected)));
        services.AddSingleton<AddonStringsOperation.TextFormatter>();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        GeneratedOperationInvoker invoker = new();
        bool invoked = invoker.TryInvoke(
            "addon.strings",
            new AddonStringsOperation.Request { AddonName = "Inventory" },
            serviceProvider,
            InvocationSurface.Protocol,
            TestContext.Current.CancellationToken,
            out ValueTask<OperationInvocationResult> invocation);

        Assert.True(invoked);

        OperationInvocationResult result = await invocation;
        Assert.Equal(typeof(AddonStringsSnapshot), result.ResultType);
        Assert.Equal(expected, Assert.IsType<AddonStringsSnapshot>(result.Result));
    }

    [Fact]
    public async Task GeneratedOperationInvoker_invokes_game_screenshot_instance_operation()
    {
        GameScreenshotSnapshot expected = new(
            DateTimeOffset.UtcNow,
            "client",
            @"C:\temp\ffxiv-client.bmp",
            1920,
            1080,
            4096,
            "Captured client screenshot.");
        ServiceCollection services = new();
        services.AddSingleton(new GameScreenshotOperation((request, _) =>
            ValueTask.FromResult(expected)));
        services.AddSingleton<GameScreenshotOperation.TextFormatter>();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        GeneratedOperationInvoker invoker = new();
        bool invoked = invoker.TryInvoke(
            "game.screenshot",
            new GameScreenshotOperation.Request { CaptureArea = "client" },
            serviceProvider,
            InvocationSurface.Protocol,
            TestContext.Current.CancellationToken,
            out ValueTask<OperationInvocationResult> invocation);

        Assert.True(invoked);

        OperationInvocationResult result = await invocation;
        Assert.Equal(typeof(GameScreenshotSnapshot), result.ResultType);
        Assert.Equal(expected, Assert.IsType<GameScreenshotSnapshot>(result.Result));
    }

    [Fact]
    public async Task GeneratedOperationInvoker_invokes_addon_tree_instance_operation()
    {
        AddonTreeSnapshot expected = new(
            "Inventory",
            DateTimeOffset.UtcNow,
            [new AddonTreeNode(1, "addon", true, 0, 0, 100, 100, "Inventory", [])]);
        ServiceCollection services = new();
        services.AddSingleton(new AddonTreeOperation((request, _) =>
            ValueTask.FromResult(expected)));
        services.AddSingleton<AddonTreeOperation.TextFormatter>();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        GeneratedOperationInvoker invoker = new();
        bool invoked = invoker.TryInvoke(
            "addon.tree",
            new AddonTreeOperation.Request { AddonName = "Inventory" },
            serviceProvider,
            InvocationSurface.Protocol,
            TestContext.Current.CancellationToken,
            out ValueTask<OperationInvocationResult> invocation);

        Assert.True(invoked);

        OperationInvocationResult result = await invocation;
        Assert.Equal(typeof(AddonTreeSnapshot), result.ResultType);
        Assert.Equal(expected, Assert.IsType<AddonTreeSnapshot>(result.Result));
    }

    [Fact]
    public async Task GeneratedOperationInvoker_invokes_unsafe_invoke_plugin_ipc_operation()
    {
        UnsafeInvokePluginIpcResult expected = new(
            "vnavmesh.Nav.IsReady",
            true,
            null,
            "bool",
            "true",
            "IPC 'vnavmesh.Nav.IsReady' returned true.");
        ServiceCollection services = new();
        services.AddSingleton(new UnsafeInvokePluginIpcOperation((request, _) => ValueTask.FromResult(expected)));
        services.AddSingleton<UnsafeInvokePluginIpcOperation.TextFormatter>();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        GeneratedOperationInvoker invoker = new();
        bool invoked = invoker.TryInvoke(
            "unsafe.invoke.plugin-ipc",
            new UnsafeInvokePluginIpcOperation.Request
            {
                Callgate = "vnavmesh.Nav.IsReady",
                ResultKind = "bool"
            },
            serviceProvider,
            InvocationSurface.Protocol,
            TestContext.Current.CancellationToken,
            out ValueTask<OperationInvocationResult> invocation);

        Assert.True(invoked);

        OperationInvocationResult result = await invocation;
        Assert.Equal(typeof(UnsafeInvokePluginIpcResult), result.ResultType);
        Assert.Equal(expected, Assert.IsType<UnsafeInvokePluginIpcResult>(result.Result));
    }
}



