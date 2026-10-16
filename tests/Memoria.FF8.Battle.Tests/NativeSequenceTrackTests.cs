using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Battle.Tests;

public sealed class NativeSequenceTrackTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SequenceCallbackProgressAndMessagesSurviveRestoreWithoutReplayingTheInitialCue(bool gameplayWait)
    {
        NativeBattleSequenceTrack track = new(1, [0x91, 0, 30, 0xA1, 0xA1, 0x91, 1, 30, 0xA1, 0xAA, 0xA2], [new(0, 0, 30), new(2, 1, 30)], 3);
        NativeBattleAiDefinition ai = new([[5, 1, 7, 1, 6, 0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(8, 1, 0), 48).ToArray(), 0, ["First", "Second"]) { SequenceTracks = [track] };
        BattleNativeAction action = Action(0) with { ActionId = 1 };
        BattleSession battle = new(new BattleDefinition
        {
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeActions = [action],
            Combatants =
            [
                new() { Id = "player", Name = "Player", IsPlayer = true, MaximumHp = 1000, CurrentHp = 1000, Speed = 1 },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000, Speed = 1, NativeAi = ai }
            ]
        }, BattleAtbMode.Normal);
        battle.Tick();
        Assert.Equal((byte)1, battle.NativeActionPackets.ActionCount);
        Assert.Null(battle.ActiveAction);
        battle.Tick();
        Assert.Equal(3, battle.ActiveAction!.RemainingFrames);
        Assert.Equal("First", Assert.Single(battle.Events, entry => entry.Kind == "NativeText").Message);
        Assert.Equal((byte)1, Assert.Single(battle.Events, entry => entry.Kind == "Windup").NativeSequence);
        if (gameplayWait)
        {
            BattleSnapshot waiting = battle.Capture();
            battle = BattleSession.Restore(waiting with { NativeScriptState = waiting.NativeScriptState with { Tasks = waiting.NativeScriptState.Tasks.AppendWait(5) } });
        }
        BattleSession restored = BattleSession.Restore(battle.Capture());
        for (int index = 0; index < 3; index++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Equal(gameplayWait ? BattleActionPhase.Windup : BattleActionPhase.Recovery, battle.ActiveAction!.Phase);
        Assert.Equal(3, battle.ActiveAction.NativeSequenceFrame);
        Assert.Equal(new[] { "First", "Second" }, battle.Events.Where(entry => entry.Kind == "NativeText").Select(entry => entry.Message));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        if (gameplayWait)
        {
            // The callback clears DFD after its zero-count pass; gameplay
            // resumes on the following frame rather than that cleanup pass.
            for (int frame = 0; frame < 4; frame++)
            {
                battle.Tick();
                restored.Tick();
            }
            Assert.Equal(BattleActionPhase.Recovery, battle.ActiveAction!.Phase);
            Assert.Equal(2, battle.Events.Count(entry => entry.Kind == "NativeText"));
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        }
    }

    [Fact]
    public void NativeGraphicsPacketKeepsItsMagicByteSequenceAndPreviousTargetMaskAcrossRestore()
    {
        NativeBattleAiDefinition ai = new([[5, 2, 30, 143, 0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []) { SequenceTracks = [new(2, [0xA1, 0xA1, 0xAA, 0xA2], [], 2)] };
        BattleSession battle = new(new BattleDefinition
        {
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeActions = [Action(0) with { ActionId = 1, AttackFlags = 0x80 }],
            Combatants =
            [
                new() { Id = "player", Name = "Player", IsPlayer = true, MaximumHp = 1000, CurrentHp = 1000, Speed = 1 },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000, Speed = 1, NativeAi = ai }
            ]
        }, BattleAtbMode.Normal);
        battle.Tick();
        BattleSnapshot queued = battle.Capture();
        BattleAttackCommand packet = Assert.Single(queued.QueuedActions, command => command.NativeHeaderIndex is not null);
        Assert.Equal((byte)252, packet.NativeQueuedCommand);
        Assert.Equal(1, packet.ActionId);
        Assert.Equal((byte)143, packet.NativeGraphic!.MagicId);
        Assert.Equal((ushort)0x4000, packet.NativeTargetMask);
        Assert.Equal((ushort)0, queued.Combatants[1].NativeParticipant!.PreviousTargetMask);
        BattleSession restored = BattleSession.Restore(queued);
        string random = JsonSerializer.Serialize(queued.Random);
        for (int frame = 0; frame < 3; frame++)
        {
            restored.Tick();
        }
        Assert.Equal((byte)2, restored.ActiveAction!.NativeSequence);
        Assert.Equal((byte)143, Assert.Single(restored.Events, entry => entry.Kind == "Windup").NativeGraphicMagicId);
        Assert.DoesNotContain(restored.Events, entry => entry.Kind is "Damage" or "Healing");
        Assert.Equal(random, JsonSerializer.Serialize(restored.Capture().Random));
        Assert.Null(restored.Capture().Combatants[0].NativeParticipant!.LastAction);
        Assert.Null(restored.Capture().Combatants[1].NativeParticipant!.LastAction);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(31)]
    public void NativeUnassignedAttackTypesCommitZeroWithoutStatusOrRandomConsumption(byte attackType)
    {
        BattleNativeAction native = Action(attackType) with { Statuses0 = 2, StatusAttack = 255 };
        BattleSession battle = new(new BattleDefinition
        {
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeActions = [native],
            Combatants =
            [
                new() { Id = "player", Name = "Player", IsPlayer = true, MaximumHp = 1000, CurrentHp = 1000, Speed = 1, InitialAtb = BattleSession.AtbMaximum, NativeCommands = [new(8, "Test", 0x50, 0)] },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000, Speed = 1 }
            ]
        }, BattleAtbMode.Normal);
        string before = JsonSerializer.Serialize(battle.Capture().Random);
        battle.QueueNativePlayerCommand("player", "enemy", 8, 0);
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames; frame++)
        {
            battle.Tick();
        }
        Assert.Equal(1000, battle.Combatants[1].CurrentHp);
        Assert.Equal((ushort)0, battle.Capture().Combatants[1].NativeStatus!.Saved);
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture().Random));
    }

    private static BattleNativeAction Action(byte type)
    {
        return new(8, 0, 2, 0) { Name = "Control", MagicId = 0, AttackType = type, Power = 0, HitCount = 1, TargetFlags = 0x50, AttackFlags = 3, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = new byte[20].ToImmutableArray(), SupplementalRecord = [] };
    }
}
