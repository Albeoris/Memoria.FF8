using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7GuestPartialMagicUiTests
{
    [Theory]
    [InlineData(0x20000u, 2, 1)]
    [InlineData(0x40000u, 3, 1)]
    [InlineData(0x40000u, 3, 2)]
    public void Card_submits_only_selected_casts_even_when_confirm_is_also_pressed(uint status, int limit, int selectedCount)
    {
        using CrossoverGuestStatusHostTests.Fixture fixture = new();
        fixture.Ready(1);
        SetStatus(fixture.Host, status);
        ushort cure = fixture.Spell("Cure");
        Ff7Scene scene = CreateScene(fixture.Host, cure, limit, Enumerable.Repeat((ushort)1, selectedCount).ToList());
        Set(scene, "semanticInput", new InputFrame(0, 0, true, false, false, false, CardChallenge: true));
        string stock = JsonSerializer.Serialize(fixture.Providers[1].WorkingStock);

        ReadCommand(scene, fixture.Host);

        Assert.Null(Get(scene, "selectedBattleCommand"));
        Ff7BattleQueuedCommand queued = Assert.Single(fixture.Host.Capture().Queue.Ready.OfType<Ff7BattleQueuedCommand>());
        Assert.Equal((ushort)1, queued.TargetMask);
        if (selectedCount > 1)
        {
            Assert.Equal(Enumerable.Repeat((ushort)1, selectedCount), Assert.Single(fixture.Host.Capture().GuestMagicSequences).Targets);
        }
        else
        {
            Assert.Empty(fixture.Host.Capture().GuestMagicSequences);
        }
        Assert.Equal(stock, JsonSerializer.Serialize(fixture.Providers[1].WorkingStock));
    }

    [Fact]
    public void Card_with_no_selected_casts_keeps_target_selection_open()
    {
        using CrossoverGuestStatusHostTests.Fixture fixture = new();
        fixture.Ready(1);
        SetStatus(fixture.Host, 0x40000);
        Ff7Scene scene = CreateScene(fixture.Host, fixture.Spell("Cure"), 3, []);
        Set(scene, "semanticInput", new InputFrame(0, 0, false, false, false, false, CardChallenge: true));
        string before = JsonSerializer.Serialize(fixture.Host.Capture());

        ReadCommand(scene, fixture.Host);

        Assert.NotNull(Get(scene, "selectedBattleCommand"));
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Host.Capture()));
    }

    [Fact]
    public void Selecting_the_last_cast_still_submits_automatically()
    {
        using CrossoverGuestStatusHostTests.Fixture fixture = new();
        fixture.Ready(1);
        SetStatus(fixture.Host, 0x40000);
        List<ushort> targets = [1, 1];
        Ff7Scene scene = CreateScene(fixture.Host, fixture.Spell("Cure"), 3, targets);
        Set(scene, "semanticInput", new InputFrame(0, 0, true, false, false, false));

        ReadCommand(scene, fixture.Host);

        Assert.Null(Get(scene, "selectedBattleCommand"));
        Assert.Equal(new ushort[] { 1, 1, 2 }, Assert.Single(fixture.Host.Capture().GuestMagicSequences).Targets);
    }

    [Fact]
    public void Rejected_partial_selection_preserves_targets_and_turn()
    {
        using CrossoverGuestStatusHostTests.Fixture fixture = new();
        fixture.Ready(1);
        SetStatus(fixture.Host, 0x40000);
        List<ushort> targets = [0];
        Ff7Scene scene = CreateScene(fixture.Host, fixture.Spell("Cure"), 3, targets);
        Set(scene, "semanticInput", new InputFrame(0, 0, false, false, false, false, CardChallenge: true));
        string before = JsonSerializer.Serialize(fixture.Host.Capture());

        ReadCommand(scene, fixture.Host);

        Assert.NotNull(Get(scene, "selectedBattleCommand"));
        Assert.Equal(new ushort[] { 0 }, targets);
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Host.Capture()));
    }

    private static Ff7Scene CreateScene(Ff7BattleSession battle, ushort ability, int limit, List<ushort> targets)
    {
        Ff7Scene scene = (Ff7Scene)RuntimeHelpers.GetUninitializedObject(typeof(Ff7Scene));
        Ff7BattleCommandOption option = Assert.Single(battle.Commands(1), option => option.Command == Ff7BattleCommand.Magic && option.Argument == ability);
        Set(scene, "selectedBattleCommand", option);
        Set(scene, "battleMagicCastCount", limit);
        Set(scene, "battleMagicTargets", targets);
        Set(scene, "highlightedBattleTarget", (1, battle.Actors.Single(actor => actor.Actor.Slot == 1).Identity));
        Set(scene, "sharedInput", new KeyboardInput());
        Set(scene, "envelope", new CombinedSessionSave());
        return scene;
    }

    private static void SetStatus(Ff7BattleSession battle, uint status)
    {
        Ff7BattleSnapshot state = battle.Capture() with { GuestOriginStatuses = [new(1, status)] };
        typeof(Ff7BattleSession).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(battle, state);
    }

    private static void ReadCommand(Ff7Scene scene, Ff7BattleSession battle)
    {
        typeof(Ff7Scene).GetMethod("ReadBattleCommand", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(scene, [battle, 1, false]);
    }

    private static object? Get(Ff7Scene scene, string field) => typeof(Ff7Scene).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(scene);

    private static void Set(Ff7Scene scene, string field, object value)
    {
        typeof(Ff7Scene).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(scene, value);
    }
}
