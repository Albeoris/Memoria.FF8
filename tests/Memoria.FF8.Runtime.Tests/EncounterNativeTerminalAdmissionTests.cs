using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class EncounterNativeTerminalAdmissionTests
{
    [Fact]
    public void DeferredPartyDialogueConsumesItsNativeCallbackWithoutAnAuthoredAttack()
    {
        BattleSession battle = Create(0, 1);
        Assert.True(Admit(battle, "original"));
        Assert.True(battle.CaptureEncounterNativeEvents()["original"].PartyPending);
        Assert.Empty(battle.EncounterPolicies!.Cursors);
        Assert.DoesNotContain(battle.QueuedActions, action => action.NativeQueuedCommand is not null);
        Assert.DoesNotContain(battle.Events, entry => entry.Kind == "EncounterPolicy");

        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        Assert.True(Admit(restored, "original"));
        BattleAttackCommand terminal = Assert.Single(restored.QueuedActions, action => action.NativeQueuedCommand == 8);
        Assert.Equal((ushort)207, terminal.NativeAction!.ActionId);
        Assert.Equal((byte)15, terminal.NativeSequence);
        Assert.Equal("original", terminal.TargetId);
        Assert.True(restored.EncounterPolicies!.TerminalStarted);
        Assert.Empty(restored.EncounterPolicies.Cursors);
    }

    [Theory]
    [InlineData(249, 0, true)]
    [InlineData(250, 0, false)]
    [InlineData(0, 1, true)]
    public void TerminalUsesSquallModelAndStrictQuarterHpEvenWhenSquallIsKo(int squallHp, ushort squallStatus, bool expected)
    {
        BattleSession battle = Create(squallHp, squallStatus);
        Assert.True(Admit(battle, "original"));
        Assert.Equal(expected, Invoke<bool>(battle, "TryRunEncounterNativeTerminal", Actor(battle, "original"), 1));
        Assert.Equal(expected, battle.EncounterPolicies!.TerminalStarted);
    }

    [Fact]
    public void ThreeNativeLivingPartyMembersPreventTheLowHpTerminal()
    {
        BattleSession battle = Create(0, 0, true);
        Assert.False(Invoke<bool>(battle, "TryRunEncounterNativeTerminal", Actor(battle, "original"), 1));
        Assert.Empty(battle.CaptureEncounterNativeEvents());
        Assert.DoesNotContain(battle.QueuedActions, action => action.NativeQueuedCommand == 8);
    }

    [Fact]
    public void TerminalAdmissionBlocksFurtherOriginalAndCopyPolicyActionsAcrossJsonRestore()
    {
        BattleSession battle = Create(0, 1);
        Assert.True(Admit(battle, "original"));
        Assert.True(Admit(battle, "original"));
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        int commands = restored.QueuedActions.Count;
        Assert.True(Admit(restored, "original"));
        Assert.True(Admit(restored, "copy"));
        Assert.Equal(commands, restored.QueuedActions.Count);
        Assert.Empty(restored.EncounterPolicies!.Cursors);
        Assert.DoesNotContain(restored.Events, entry => entry.Kind == "EncounterPolicy");
    }

    [Fact]
    public void ZeroHpWithoutNativeKoRemainsDistinctFromAnAdmittedPartyDeath()
    {
        BattleSession battle = Create(0, 0, true);
        BattleCombatant squall = Actor(battle, "squall");
        Assert.Equal(0, squall.CurrentHp);
        Assert.True(squall.IsAlive);
        Assert.False(Invoke<bool>(battle, "TryRunEncounterNativeTerminal", Actor(battle, "original"), 1));
    }

    private static bool Admit(BattleSession battle, string id) => Invoke<bool>(battle, "TryRunEncounterPolicy", Actor(battle, id), 1);

    private static BattleCombatant Actor(BattleSession battle, string id) => battle.Combatants.Single(actor => actor.Id == id);

    private static T Invoke<T>(BattleSession battle, string method, params object[] arguments)
    {
        return (T)typeof(BattleSession).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, arguments)!;
    }

    private static BattleSession Create(int squallHp, ushort squallStatus, bool thirdLiving = false)
    {
        NativeBattleAiAbility[] abilities = Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray();
        abilities[10] = new(8, 15, 207);
        NativeBattleAiDefinition ai = new([[0], [0], [0], [0], [0]], abilities, 0, []);
        EncounterCompiledDefinition mod = new(EncounterBindingResolver.Disc1Edea, "Classic", "terminal-fixture", "original", 11,
            ImmutableDictionary<string, EncounterCompiledPolicy>.Empty,
            ImmutableDictionary<string, string>.Empty.Add("original", "policy").Add("copy", "policy"),
            ImmutableDictionary<string, EncounterCompiledTemplate>.Empty,
            ImmutableDictionary<string, ImmutableArray<string>>.Empty,
            ImmutableDictionary<string, EncounterCompiledPresentation>.Empty,
            ImmutableDictionary<string, EncounterCompiledDamageRoute>.Empty,
            ImmutableDictionary<string, EncounterCompiledEventGate>.Empty.Add("taunts", new("original", "nativeTaunts", 5)),
            ImmutableDictionary<string, EncounterCompiledPlacement>.Empty,
            ImmutableDictionary<string, double>.Empty,
            new([new(0, 1)], [new(1, 1), new(2, 1)], [new(3, 1)], [new(4, 1)]));
        BattleNativeAction terminal = new(8, 207, 0, 207) { MagicId = 0, AttackType = 0, Power = 0, HitCount = 1, TargetFlags = 0, AttackFlags = 3, DamagePresentation = 0, Element = 0, ElementPercent = null, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, Statuses0 = 0, Statuses1 = 0, StatusAttack = 0, RawRecord = [0], SupplementalRecord = [] };
        BattleDefinition definition = new()
        {
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            EncounterMod = mod,
            NativeActions = [terminal],
            Combatants =
            [
                new() { Id = "ally", Name = "Ally", IsPlayer = true, ModelId = 2, MaximumHp = 1000, CurrentHp = 1000 },
                new() { Id = "squall", Name = "Squall", IsPlayer = true, ModelId = 0, MaximumHp = 1000, CurrentHp = squallHp, NativeStatus0 = squallStatus },
                new() { Id = "third", Name = "Third", IsPlayer = true, ModelId = 4, MaximumHp = 1000, CurrentHp = thirdLiving ? 1000 : 0, NativeStatus0 = thirdLiving ? (ushort)0 : (ushort)1 },
                new() { Id = "original", Name = "Original", ModelId = 88, MaximumHp = 1000, CurrentHp = 1000, NativeAi = ai, NativeCombatantFlags = 0x11 },
                new() { Id = "copy", Name = "Copy", ModelId = 88, MaximumHp = 1000, CurrentHp = 1000, NativeAi = ai, NativeCombatantFlags = 0x11 }
            ]
        };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        typeof(BattleSession).GetMethod("WriteNativeActorVariable", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, [Actor(battle, "original"), 220, 1u, false]);
        return battle;
    }
}
