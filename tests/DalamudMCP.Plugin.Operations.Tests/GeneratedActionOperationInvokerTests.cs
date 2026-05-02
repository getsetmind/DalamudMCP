using Manifold;
using Manifold.Generated;
using Microsoft.Extensions.DependencyInjection;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class GeneratedActionOperationInvokerTests
{
    [Fact]
    public async Task GeneratedOperationInvoker_invokes_target_object_operation()
    {
        TargetObjectResult expected = new("0x123", true, null, "0x123", "Summoning Bell", "EventObj", "Targeted Summoning Bell.");
        ServiceCollection services = new();
        services.AddSingleton(new TargetObjectOperation((request, _) => ValueTask.FromResult(expected)));
        services.AddSingleton<TargetObjectOperation.TextFormatter>();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        GeneratedOperationInvoker invoker = new();
        bool invoked = invoker.TryInvoke("target.object", new TargetObjectOperation.Request { GameObjectId = "0x123" }, serviceProvider, InvocationSurface.Protocol, TestContext.Current.CancellationToken, out ValueTask<OperationInvocationResult> invocation);

        Assert.True(invoked);
        OperationInvocationResult result = await invocation;
        Assert.Equal(expected, Assert.IsType<TargetObjectResult>(result.Result));
    }

    [Fact]
    public async Task GeneratedOperationInvoker_invokes_interact_with_target_operation()
    {
        InteractWithTargetResult expected = new("0x123", true, null, "0x123", "Summoning Bell", "EventObj", 2.3, "Interaction started with Summoning Bell.");
        ServiceCollection services = new();
        services.AddSingleton(new InteractWithTargetOperation((request, _) => ValueTask.FromResult(expected)));
        services.AddSingleton<InteractWithTargetOperation.TextFormatter>();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        GeneratedOperationInvoker invoker = new();
        bool invoked = invoker.TryInvoke("interact.with.target", new InteractWithTargetOperation.Request { ExpectedGameObjectId = "0x123" }, serviceProvider, InvocationSurface.Protocol, TestContext.Current.CancellationToken, out ValueTask<OperationInvocationResult> invocation);

        Assert.True(invoked);
        OperationInvocationResult result = await invocation;
        Assert.Equal(expected, Assert.IsType<InteractWithTargetResult>(result.Result));
    }

    [Fact]
    public async Task GeneratedOperationInvoker_invokes_move_to_entity_operation()
    {
        MoveToEntityResult expected = new("0x123", true, null, "0x123", "Summoning Bell", "EventObj", new MoveDestination(1, 2, 3), "Movement started toward Summoning Bell.");
        ServiceCollection services = new();
        services.AddSingleton(new MoveToEntityOperation((request, _) => ValueTask.FromResult(expected)));
        services.AddSingleton<MoveToEntityOperation.TextFormatter>();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        GeneratedOperationInvoker invoker = new();
        bool invoked = invoker.TryInvoke("move.to.entity", new MoveToEntityOperation.Request { GameObjectId = "0x123" }, serviceProvider, InvocationSurface.Protocol, TestContext.Current.CancellationToken, out ValueTask<OperationInvocationResult> invocation);

        Assert.True(invoked);
        OperationInvocationResult result = await invocation;
        Assert.Equal(expected, Assert.IsType<MoveToEntityResult>(result.Result));
    }

    [Fact]
    public async Task GeneratedOperationInvoker_invokes_move_to_nearby_interactable_operation()
    {
        MoveToEntityResult expected = new("bell", true, null, "0x123", "Summoning Bell", "EventObj", new MoveDestination(1, 2, 3), "Movement started toward Summoning Bell.");
        ServiceCollection services = new();
        services.AddSingleton(new MoveToNearbyInteractableOperation((request, _) => ValueTask.FromResult(expected)));
        services.AddSingleton<MoveToNearbyInteractableOperation.TextFormatter>();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        GeneratedOperationInvoker invoker = new();
        bool invoked = invoker.TryInvoke("move.to.nearby.interactable", new MoveToNearbyInteractableOperation.Request { NameContains = "bell" }, serviceProvider, InvocationSurface.Protocol, TestContext.Current.CancellationToken, out ValueTask<OperationInvocationResult> invocation);

        Assert.True(invoked);
        OperationInvocationResult result = await invocation;
        Assert.Equal(expected, Assert.IsType<MoveToEntityResult>(result.Result));
    }

    [Fact]
    public async Task GeneratedOperationInvoker_invokes_teleport_to_aetheryte_operation()
    {
        TeleportToAetheryteResult expected = new("limsa", true, null, 8u, "Limsa Lominsa", "Lower La Noscea", "Teleport started to Limsa Lominsa.");
        ServiceCollection services = new();
        services.AddSingleton(new TeleportToAetheryteOperation((request, _) => ValueTask.FromResult(expected)));
        services.AddSingleton<TeleportToAetheryteOperation.TextFormatter>();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        GeneratedOperationInvoker invoker = new();
        bool invoked = invoker.TryInvoke("teleport.to.aetheryte", new TeleportToAetheryteOperation.Request { Query = "limsa" }, serviceProvider, InvocationSurface.Protocol, TestContext.Current.CancellationToken, out ValueTask<OperationInvocationResult> invocation);

        Assert.True(invoked);
        OperationInvocationResult result = await invocation;
        Assert.Equal(expected, Assert.IsType<TeleportToAetheryteResult>(result.Result));
    }

    [Fact]
    public async Task GeneratedOperationInvoker_invokes_duty_action_operation()
    {
        DutyActionResult expected = new(1, true, null, 777u, "Executed duty action slot 1.");
        ServiceCollection services = new();
        services.AddSingleton(new DutyActionOperation((request, _) => ValueTask.FromResult(expected)));
        services.AddSingleton<DutyActionOperation.TextFormatter>();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        GeneratedOperationInvoker invoker = new();
        bool invoked = invoker.TryInvoke("duty.action", new DutyActionOperation.Request { Slot = 1 }, serviceProvider, InvocationSurface.Protocol, TestContext.Current.CancellationToken, out ValueTask<OperationInvocationResult> invocation);

        Assert.True(invoked);
        OperationInvocationResult result = await invocation;
        Assert.Equal(expected, Assert.IsType<DutyActionResult>(result.Result));
    }

    [Fact]
    public async Task GeneratedOperationInvoker_invokes_addon_callback_values_operation()
    {
        AddonCallbackValuesResult expected = new("Inventory", [8, 1], true, null, "Sent callback values [8, 1] to Inventory.");
        ServiceCollection services = new();
        services.AddSingleton(new AddonCallbackValuesOperation((request, _) => ValueTask.FromResult(expected)));
        services.AddSingleton<AddonCallbackValuesOperation.TextFormatter>();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        GeneratedOperationInvoker invoker = new();
        bool invoked = invoker.TryInvoke("addon.callback.values", new AddonCallbackValuesOperation.Request { AddonName = "Inventory", Values = [8, 1] }, serviceProvider, InvocationSurface.Protocol, TestContext.Current.CancellationToken, out ValueTask<OperationInvocationResult> invocation);

        Assert.True(invoked);
        OperationInvocationResult result = await invocation;
        Assert.Equal(expected, Assert.IsType<AddonCallbackValuesResult>(result.Result));
    }

    [Fact]
    public async Task GeneratedOperationInvoker_invokes_addon_input_operation()
    {
        AddonInputResult expected = new("Inventory", "gamepad", 1, false, true, null, "Sent gamepad input 1 (down) to Inventory.");
        ServiceCollection services = new();
        services.AddSingleton(new AddonInputOperation((request, _) => ValueTask.FromResult(expected)));
        services.AddSingleton<AddonInputOperation.TextFormatter>();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        GeneratedOperationInvoker invoker = new();
        bool invoked = invoker.TryInvoke("addon.input", new AddonInputOperation.Request { AddonName = "Inventory", InputType = "gamepad", InputId = 1 }, serviceProvider, InvocationSurface.Protocol, TestContext.Current.CancellationToken, out ValueTask<OperationInvocationResult> invocation);

        Assert.True(invoked);
        OperationInvocationResult result = await invocation;
        Assert.Equal(expected, Assert.IsType<AddonInputResult>(result.Result));
    }

    [Fact]
    public async Task GeneratedOperationInvoker_invokes_addon_event_operation()
    {
        AddonEventResult expected = new("Inventory", "buttonClick", 7, 2, 40, true, null, "Dispatched buttonClick event 7 to Inventory via collision[2]; node dispatch handled it.");
        ServiceCollection services = new();
        services.AddSingleton(new AddonEventOperation((request, _) => ValueTask.FromResult(expected)));
        services.AddSingleton<AddonEventOperation.TextFormatter>();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        GeneratedOperationInvoker invoker = new();
        bool invoked = invoker.TryInvoke("addon.event", new AddonEventOperation.Request { AddonName = "Inventory", EventType = "buttonClick", EventParam = 7 }, serviceProvider, InvocationSurface.Protocol, TestContext.Current.CancellationToken, out ValueTask<OperationInvocationResult> invocation);

        Assert.True(invoked);
        OperationInvocationResult result = await invocation;
        Assert.Equal(expected, Assert.IsType<AddonEventResult>(result.Result));
    }

    [Fact]
    public async Task GeneratedOperationInvoker_invokes_addon_select_menu_item_operation()
    {
        AddonSelectMenuItemResult expected = new(
            "TelepotTown",
            "マーケット",
            "マーケット（革細工師ギルド前）",
            3,
            "telepot-town-agent",
            true,
            null,
            "Selected 'マーケット（革細工師ギルド前）' from TelepotTown using teleport index 3.");
        ServiceCollection services = new();
        services.AddSingleton(new AddonSelectMenuItemOperation((request, _) => ValueTask.FromResult(expected)));
        services.AddSingleton<AddonSelectMenuItemOperation.TextFormatter>();
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        GeneratedOperationInvoker invoker = new();
        bool invoked = invoker.TryInvoke(
            "addon.select.menu-item",
            new AddonSelectMenuItemOperation.Request
            {
                AddonName = "TelepotTown",
                Label = "マーケット",
                ContainsMatch = true
            },
            serviceProvider,
            InvocationSurface.Protocol,
            TestContext.Current.CancellationToken,
            out ValueTask<OperationInvocationResult> invocation);

        Assert.True(invoked);
        OperationInvocationResult result = await invocation;
        Assert.Equal(expected, Assert.IsType<AddonSelectMenuItemResult>(result.Result));
    }
}



