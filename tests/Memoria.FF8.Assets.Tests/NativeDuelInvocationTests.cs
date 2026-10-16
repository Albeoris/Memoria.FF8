using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeDuelInvocationTests
{
    [Fact]
    public void A_completed_finisher_queues_native239_then_releases_the_Duel_process()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = CreateBattle(game, assets);
        battle.QueueNativePlayerCommand("zell", "enemy", 11, 0);
        Until(battle, () => battle.GetNativeDuel("zell")?.Phase == BattleNativeDuelPhase.Ready);
        BattleNativeLimitDefinition rules = assets.ReadNativeLimitDefinition(1);
        byte moveSet = (byte)Enumerable.Range(0, rules.DuelMoveSets.Length / 4).First(set => NativeBattleDuelRules.Choices(rules, (byte)set, 0, assets.NativeActions).Any(choice => choice.Finisher));
        BattleNativeLimitState selected = Assert.Single(battle.Capture().NativeLimits);
        battle = BattleSession.Restore(battle.Capture() with { NativeLimits = [selected with { Duel = selected.Duel! with { MoveSetId = moveSet } }] });
        BattleNativeDuelChoice finisher = battle.GetNativeDuelChoices("zell").First(choice => choice.Finisher);
        int hp = battle.Combatants[1].CurrentHp;

        foreach (ushort button in finisher.Buttons)
        {
            battle.AdvanceNativeDuelUi(rawPulse: button);
        }

        BattleAttackCommand queued = Assert.Single(battle.QueuedActions);
        Assert.Equal((byte)239, queued.NativeQueuedCommand);
        Assert.Equal((ushort)finisher.ActionId, queued.NativeAction!.ActionId);
        Assert.Equal(NativeLimitCallback.DuelFinisher, queued.NativeLimitCallback);
        Until(battle, () => battle.GetNativeDuel("zell") is null);
        Assert.True(battle.Combatants[1].CurrentHp < hp);
        Assert.False(battle.Combatants[0].IsBusy);
    }

    [Fact]
    public void Duel_restores_a_partial_native_input_ring_and_queues_only_a_completed_combo()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = CreateBattle(game, assets);
        BattleActionDefinition invocation = Assert.Single(battle.GetNativePlayerActionChoices("zell", true));
        Assert.Null(invocation.NativeAction);
        battle.QueueAction("zell", "enemy", BattleAbility.LimitBreak, invocation.Id);
        BattleAttackCommand invocationPacket = Assert.Single(battle.QueuedActions);
        Assert.Equal((byte)11, invocationPacket.NativeQueuedCommand);
        Assert.NotNull(invocationPacket.NativeDuel);
        Assert.Null(battle.GetNativeDuel("zell"));
        Assert.Equal(1, battle.NativeCommandBanks.CountActor(0));
        string queuedSnapshot = JsonSerializer.Serialize(battle.Capture());
        battle = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(queuedSnapshot)!);
        Assert.Equal(queuedSnapshot, JsonSerializer.Serialize(battle.Capture()));
        Until(battle, () => battle.GetNativeDuel("zell")?.Phase == BattleNativeDuelPhase.Ready);
        BattleNativeDuelChoice choice = battle.GetNativeDuelChoices("zell")[0];
        Assert.False(choice.Finisher);
        Assert.True(choice.Buttons.Length > 1);
        long frame = battle.Frame;
        battle.AdvanceNativeDuelUi(rawPulse: choice.Buttons[0]);
        Assert.Empty(battle.QueuedActions);
        string snapshot = JsonSerializer.Serialize(battle.Capture());

        battle = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(snapshot)!);
        Assert.Equal(snapshot, JsonSerializer.Serialize(battle.Capture()));
        foreach (ushort button in choice.Buttons.Skip(1))
        {
            battle.AdvanceNativeDuelUi(rawPulse: button);
        }

        BattleAttackCommand queued = Assert.Single(battle.QueuedActions);
        Assert.Equal((ushort)choice.ActionId, queued.NativeAction!.ActionId);
        Assert.Equal(NativeLimitCallback.DuelEffect, queued.NativeLimitCallback);
        Assert.Equal(frame, battle.Frame);
        short remaining = battle.GetNativeDuel("zell")!.RemainingTimer;
        battle.AdvanceNativeDuelUi(10);
        Assert.Equal(remaining, battle.GetNativeDuel("zell")!.RemainingTimer);
        Until(battle, () => battle.GetNativeDuel("zell")?.Phase == BattleNativeDuelPhase.Ready);
        Assert.Equal(choice.MoveSetId, battle.GetNativeDuel("zell")!.MoveSetId);
        Assert.All(battle.GetNativeDuel("zell")!.InputRing, button => Assert.Equal((ushort)0, button));
    }

    [Fact]
    public void Manual_native_matching_accepts_an_unlearned_finisher_and_uses_its_exact_button_words()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleNativeLimitDefinition rules = assets.ReadNativeLimitDefinition(1);
        BattleNativeDuelChoice finisher = Enumerable.Range(0, rules.DuelMoveSets.Length / 4).SelectMany(set => NativeBattleDuelRules.Choices(rules, (byte)set, 0, assets.NativeActions)).First(choice => choice.Finisher);
        ushort[] ring = new ushort[8];
        byte cursor = 6;
        foreach (ushort button in finisher.Buttons)
        {
            cursor = (byte)((cursor + 1) & 7);
            ring[cursor] = button;
        }

        Assert.False(finisher.Learned);
        Assert.All(finisher.Buttons, button => Assert.Equal(0, button & 0xF00));
        Assert.True(NativeBattleDuelRules.Matches(finisher, ring.ToImmutableArray(), cursor));
        ring[cursor] ^= 0x80;
        Assert.False(NativeBattleDuelRules.Matches(finisher, ring.ToImmutableArray(), cursor));
    }

    [Fact]
    public void Duel_timeout_changes_no_gameplay_clock_or_hp_and_automatic_mode_uses_the_owned_UI_draw()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession manual = CreateBattle(game, assets);
        manual.QueueNativePlayerCommand("zell", "enemy", 11, 0);
        Until(manual, () => manual.GetNativeDuel("zell")?.Phase == BattleNativeDuelPhase.Ready);
        BattleSnapshot before = manual.Capture();

        manual.AdvanceNativeDuelUi(manual.GetNativeDuel("zell")!.RemainingTimer);

        Assert.Null(manual.GetNativeDuel("zell"));
        Assert.Equal(before.Frame, manual.Frame);
        Assert.Equal(before.Combatants[1].CurrentHp, manual.Combatants[1].CurrentHp);
        Assert.False(manual.Combatants[0].IsBusy);
        BattleSession automatic = CreateBattle(game, assets, 2, 4);
        automatic.QueueNativePlayerCommand("zell", "enemy", 11, 0);
        Until(automatic, () => automatic.GetNativeDuel("zell")?.Phase == BattleNativeDuelPhase.Ready);
        BattleSnapshot automaticBefore = automatic.Capture();
        NativeBattleUiRandomState expected = automaticBefore.NativeBattleUiRandom!.Draw(out _);
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(automaticBefore with { Definition = automaticBefore.Definition with { NativeBattleUiRandom = null }, NativeBattleUiRandom = null }));

        automatic.AdvanceNativeDuelUi(90);

        Assert.Single(automatic.QueuedActions);
        Assert.Equal(expected, automatic.Capture().NativeBattleUiRandom);
        Assert.Equal(automaticBefore.NativeCrtRandom, automatic.Capture().NativeCrtRandom);
        Assert.Equal(automaticBefore.Random, automatic.Capture().Random);
        Assert.NotEqual(BattleNativeDuelPhase.Ready, automatic.GetNativeDuel("zell")!.Phase);
    }

    [Theory]
    [InlineData(31195u, 32765, false)]
    [InlineData(29360u, 32766, true)]
    [InlineData(27525u, 32767, true)]
    public void Automatic_three_choice_boundary_preserves_the_native_UI_draw_and_unknown_stack_read(uint seed, int roll, bool invalidStackRead)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = CreateBattle(game, assets, 2, 4);
        battle.QueueNativePlayerCommand("zell", "enemy", 11, 0);
        Until(battle, () => battle.GetNativeDuel("zell")?.Phase == BattleNativeDuelPhase.Ready);
        BattleSnapshot saved = battle.Capture();
        BattleNativeLimitState selected = Assert.Single(saved.NativeLimits);
        byte moveSet = (byte)Enumerable.Range(0, saved.Definition.Combatants[0].NativeLimit!.DuelMoveSets.Length / 4).First(set => NativeBattleDuelRules.Choices(saved.Definition.Combatants[0].NativeLimit!, (byte)set, 0x3FF, assets.NativeActions).Count(choice => choice.Learned) == 3);
        battle = BattleSession.Restore(saved with { NativeBattleUiRandom = new(seed), NativeLimits = [selected with { Duel = selected.Duel! with { MoveSetId = moveSet, AutomaticCountdown = 31 } }] });
        BattleNativeDuelChoice[] choices = battle.GetNativeDuelChoices("zell").Where(choice => choice.Learned).ToArray();
        NativeBattleUiRandomState expected = new NativeBattleUiRandomState(seed).Draw(out ushort actualRoll);
        Assert.Equal(roll, actualRoll);
        BattleSnapshot before = battle.Capture();

        if (invalidStackRead)
        {
            NotSupportedException error = Assert.Throws<NotSupportedException>(() => battle.AdvanceNativeDuelUi());
            Assert.Contains("uninitialized fourth stack byte", error.Message);
            Assert.Empty(battle.QueuedActions);
        }
        else
        {
            battle.AdvanceNativeDuelUi();
            Assert.Equal(choices[2].ActionId, Assert.Single(battle.QueuedActions).ActionId);
        }

        Assert.Equal(expected, battle.Capture().NativeBattleUiRandom);
        Assert.Equal(before.NativeCrtRandom, battle.Capture().NativeCrtRandom);
        Assert.Equal(before.Random, battle.Capture().Random);
        Assert.Equal(before.Frame, battle.Frame);
        Assert.Equal(before.Combatants[1].CurrentHp, battle.Combatants[1].CurrentHp);
    }

    private static BattleSession CreateBattle(GameAssets game, BattleAssets assets, ushort options = 0, byte crisis = 1)
    {
        NativeLimitProgress progress = game.ReadNewGameLimitProgress() with { DuelFlags = 0x3FF, Options978 = options };
        BattleSession battle = new(new BattleDefinition { NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(), NativeCommandSequences = NativeBattleCommandSequenceFixture.Values, NativeActions = assets.NativeActions.ToArray(), NativeCrtRandom = NativeCrtRandomState.FromColdProcess(), NativeBattleUiRandom = new(0), LimitProgress = progress, NativeLimitsDisabled = false, Combatants = [new() { Id = "zell", Name = "Zell", IsPlayer = true, ModelId = 1, Attack = 80, Hit = 255, InitialAtb = BattleSession.AtbMaximum, NativeLimit = assets.ReadNativeLimitDefinition(1) }, new() { Id = "enemy", Name = "Enemy", MaximumHp = 50000, CurrentHp = 50000, Speed = 1, Defense = 1 }] }, BattleAtbMode.TurnBased);
        return BattleSession.Restore(battle.Capture() with { NativeLimits = [new("zell", crisis)] });
    }

    private static void Until(BattleSession battle, Func<bool> complete)
    {
        for (int frame = 0; frame < 100 && !complete(); frame++)
        {
            battle.Tick();
        }
        Assert.True(complete());
    }
}
