using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeRinoaLimitTests
{
    [Theory]
    [InlineData(1, 0xF, 0)]
    [InlineData(4, 0x5, 2)]
    [InlineData(3, 0x3, 1)]
    public void Combine_uses_the_highest_learned_finisher_at_or_below_crisis(byte crisis, byte learned, ushort expected)
    {
        Assert.Equal(expected, NativeBattleLimitRules.AngeloActionId(crisis, learned));
        Assert.Throws<InvalidDataException>(() => NativeBattleLimitRules.AngeloActionId(crisis, 0));
    }

    [Fact]
    public void Rinoa_saved_menu_gates_and_learned_finisher_own_the_queued_action()
    {
        using BattleAssets assets = InstalledAssets();
        BattleSession battle = CreateBattle(assets, 0x20, 0x50);
        string before = JsonSerializer.Serialize(battle.Capture());

        BattleActionDefinition[] choices = battle.GetNativePlayerActionChoices("rinoa", true).ToArray();

        Assert.Equal(new ushort[] { 2, 4 }, choices.Select(choice => choice.NativeAction!.ActionId));
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
        Assert.Throws<InvalidOperationException>(() => battle.QueueNativePlayerCommand("rinoa", "enemy", 19, 3));
        battle.QueueNativePlayerCommand("rinoa", "enemy", 19, 2);
        BattleAttackCommand queued = Assert.Single(battle.QueuedActions);
        Assert.Equal((byte)19, queued.NativeQueuedCommand);
        Assert.Equal((ushort)2, queued.NativeAction!.ActionId);
        Assert.Equal((ushort)0x8007, NativeBattleTargetRequestAdapter.ProjectOriginalWord(queued.TargetRequest, selected => battle.Combatants.Single(actor => actor.Id == selected.ActorId).Slot));
        string snapshot = JsonSerializer.Serialize(battle.Capture());
        Assert.Equal(snapshot, JsonSerializer.Serialize(BattleSession.Restore(battle.Capture()).Capture()));
    }

    [Fact]
    public void Angel_Wing_is_gated_by_the_saved_byte_and_keeps_its_native_self_target()
    {
        using BattleAssets assets = InstalledAssets();
        BattleSession hidden = CreateBattle(assets, 0x10, 0x10);
        Assert.Empty(hidden.GetNativePlayerActionChoices("rinoa", true));
        BattleSession battle = CreateBattle(assets, 0x30, 0x10);
        BattleActionDefinition choice = Assert.Single(battle.GetNativePlayerActionChoices("rinoa", true));

        battle.QueueNativePlayerCommand("rinoa", "ally", 19, choice.NativeAction!.ActionId);

        Assert.Equal((ushort)4, choice.NativeAction.ActionId);
        Assert.Equal((ushort)0x81, NativeBattleTargetRequestAdapter.ProjectOriginalWord(Assert.Single(battle.QueuedActions).TargetRequest, selected => battle.Combatants.Single(actor => actor.Id == selected.ActorId).Slot));
        battle.Tick();
        Assert.NotNull(battle.ActiveAction);
        Assert.Equal("rinoa", battle.ActiveAction.TargetId);
    }

    [Theory]
    [InlineData(0, 86, 6)]
    [InlineData(1, 87, 1)]
    [InlineData(2, 88, 4)]
    [InlineData(3, 89, 48)]
    public void ExpandedCombineUsesTheLearnedFinisherAndOriginalTargetContract(ushort row, ushort effect, int contacts)
    {
        using BattleAssets assets = InstalledAssets();
        BattleSession battle = CreateBattle(assets, 0x20, (byte)(0x10 | 0x10 << row), expanded: true);
        BattleActionDefinition choice = Assert.Single(battle.GetNativePlayerActionChoices("rinoa", true));
        Assert.Equal(row, choice.NativeAction!.ActionId);
        Assert.Equal(effect, choice.NativeAction.MagicId);
        Assert.Throws<NotSupportedException>(() => battle.QueueNativePlayerCommand("rinoa", "rinoa", 19, 4));
        NativeBattleRandomState before = battle.Capture().Random!;
        NativeBattleRandomState expected = row == 1 ? before.Draw(out _) : before;
        battle.QueueNativePlayerCommand("rinoa", "enemy-5", 19, row);
        BattleAttackCommand queued = Assert.Single(battle.QueuedActions);
        Assert.Null(queued.TargetRequest.OriginalProjection);
        Assert.Equal(row switch { 1 => "enemy-3", 2 => "rinoa", _ => "enemy-0" }, queued.TargetId);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(battle.Capture().Random));
        BattleSession restored = BattleSession.Restore(battle.Capture());
        for (int tick = 0; tick < 1200 && !(battle.Events.Any(item => item.Kind == "ActionComplete")); tick++)
        {
            battle.Tick();
            restored.Tick();
        }
        string?[] actual = battle.Events.Where(entry => entry.Kind == "EffectContact").Select(entry => entry.TargetId).ToArray();
        Assert.Equal(contacts, actual.Length);
        Assert.Equal(row switch
        {
            1 => ["enemy-3"],
            2 => ["rinoa", "ally", "ally-2", "ally-3"],
            _ => Enumerable.Range(0, choice.NativeAction.HitCount).SelectMany(_ => Enumerable.Range(0, 6).Select(index => $"enemy-{index}")).ToArray()
        }, actual);
        if (row == 2)
        {
            Assert.All(battle.Capture().Combatants.Take(4), actor => Assert.Equal(0x800u, actor.NativeStatus!.Battle & 0x800));
        }
        Assert.Single(battle.Events, entry => entry.Kind == "ActionComplete");
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Theory]
    [InlineData(false, (ushort)2)]
    [InlineData(true, (ushort)0)]
    public void Combine_skips_Invincible_Moon_only_when_the_rule_is_enabled_and_every_ally_used_it(bool enabled, ushort fallback)
    {
        using BattleAssets assets = InstalledAssets();
        BattleSession initial = CreateBattle(assets, 0, 0x50, invincibilityOncePerBattle: enabled);
        BattleSnapshot saved = initial.Capture();
        BattleCombatantSnapshot[] combatants = saved.Combatants.Select(actor => actor with { ReceivedInvincibility = actor.Id == "rinoa" }).ToArray();
        BattleSession battle = BattleSession.Restore(saved with { Combatants = combatants });
        Assert.Equal((ushort)2, Assert.Single(battle.GetNativePlayerActionChoices("rinoa", true)).NativeAction!.ActionId);

        combatants[1] = combatants[1] with { ReceivedInvincibility = true };
        battle = BattleSession.Restore(saved with { Combatants = combatants });
        Assert.Equal(fallback, Assert.Single(battle.GetNativePlayerActionChoices("rinoa", true)).NativeAction!.ActionId);
        if (enabled)
        {
            Assert.Throws<InvalidOperationException>(() => battle.QueueNativePlayerCommand("rinoa", "ally", 19, 2));
        }
    }

    private static BattleSession CreateBattle(BattleAssets assets, byte savedFlags, byte angeloFlags, bool expanded = false, bool invincibilityOncePerBattle = false)
    {
        NativeLimitProgress progress = new(new string('A', 64), 0, 0, angeloFlags, 0, Enumerable.Repeat((byte)0, 8).ToImmutableArray());
        BattleCombatantDefinition rinoa = new() { Id = "rinoa", Name = "Rinoa", IsPlayer = true, ModelId = 4, InitialAtb = BattleSession.AtbMaximum, NativeLimit = assets.ReadNativeLimitDefinition(4), Attack = 40, Magic = 40, NativeStatusResistances = new byte[40] };
        BattleCombatantDefinition ally = new() { Id = "ally", Name = "Ally", IsPlayer = true, NativeStatusResistances = new byte[40] };
        BattleCombatantDefinition enemy = new() { Id = "enemy", Name = "Enemy" };
        BattleCombatantDefinition[] combatants = expanded
            ? [rinoa, ally, ally with { Id = "ally-2" }, ally with { Id = "ally-3" }, .. Enumerable.Range(0, 6).Select(index => enemy with { Id = $"enemy-{index}", MaximumHp = 50000, CurrentHp = 50000, Speed = 1 })]
            : [rinoa, ally, enemy];
        BattleSession battle = new(new BattleDefinition
        {
            InvincibilityOncePerBattle = invincibilityOncePerBattle,
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values, NativeActions = assets.NativeActions.ToArray(),
            NativeSavedBattleFlags = savedFlags, NativeSavedBattleFlagsKnownMask = 255, LimitProgress = progress, NativeLimitsDisabled = false,
            NativeStatusRules = expanded ? assets.ReadNativeStatusRules(0) : null,
            InitialRandom = expanded ? NativeBattleRandomState.ForecastPhase(0) : null, Combatants = combatants
        }, BattleAtbMode.TurnBased);
        return BattleSession.Restore(battle.Capture() with { NativeLimits = [new("rinoa", 4)] });
    }

    private static BattleAssets InstalledAssets()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle resources are required.");
        return new BattleAssets(directory);
    }
}
