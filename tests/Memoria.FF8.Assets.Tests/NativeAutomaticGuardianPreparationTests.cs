using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeAutomaticGuardianPreparationTests
{
    [Fact]
    public void Callback7_prepares_original_Excalipoor_before_its_single_event_completion_and_preserves_ATB()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter encounter = assets.LoadEncounter(94);
        BattleStage stage = assets.LoadStage(encounter.StageId);
        BattleCombatantDefinition[] actors =
        [
            new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = 0, InitialAtb = BattleSession.AtbMaximum },
            new() { Id = "enemy", Name = "Ifrit", Information246 = assets.ReadEnemyNativeInformation246(61), ModelId = 61, SceneSlot = encounter.Enemies.Single(enemy => enemy.ModelId == 61).Slot, MaximumHp = 10000, CurrentHp = 10000, Speed = 1, NativeStatusResistances = new byte[40] }
        ];
        NativeBattleMusicEntryPreparation music = NativeBattleMusicFixture.Prepare(game, encounter.Id);
        BattleDefinition definition = new()
        {
            EncounterId = encounter.Id,
            StageId = encounter.StageId,
            Combatants = actors,
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeMusicProcess = music.MusicProcess,
            NativeGlobalSoundProcess = music.SoundProcess,
            NativeVibrationProcess = NativeVibrationProcessState.FromColdProcess(),
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([330]),
            NativeFieldBattleRules = 0,
            NativeActions = assets.NativeActions.ToArray(),
            ActionDefinitions = assets.ActionDefinitions.Select(action => action.Ability == BattleAbility.GuardianForce && action.Id == 0 ? action with { Level = 10 } : action).ToArray(),
            GuardianForces = [new(0, 1000, 1000, true)],
            NativeSupportRules = assets.ReadNativeSupportRules(),
            NativeSavedBattleFlags = 0,
            NativeSavedBattleFlagsKnownMask = 255,
            InitialRandom = new(Enumerable.Repeat((byte)1, 8).ToImmutableArray(), 0),
            NativeClockRules = assets.ReadNativeClockRules(),
            NativeBattleSpeed = 0,
            NativeFlags = 0x80,
            NativeEntry = new(0, 0),
            NativeGlobalRandom = new(0),
            NativeCrtRandom = new(1),
            NativeStageSource = stage.NativeSource,
            NativeStageCameraBank = stage.NativeCameraBank,
            NativeStageCameraSelectors = [encounter.PrimaryCamera, encounter.AlternativeCamera],
            NativeStageCameraControl = stage.NativeCameraControl,
            NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5),
            NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), encounter)
        };
        BattleSession entry = new(definition, BattleAtbMode.TurnBased);
        for (int callback = 0; callback < 200 && !entry.NativeEntryReady; callback++)
        {
            entry.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks(333_334));
        }
        Assert.True(entry.NativeEntryReady);
        BattleSnapshot saved = entry.Capture();
        Assert.Empty(saved.NativeGraphics!.Descriptors.Queue);
        Assert.Equal((byte)0, saved.NativeActionPackets!.ActionCount);
        NativeBattleActionHeaderSlot retainedHeader = saved.NativeActionPackets.Headers[0];
        saved = saved with { NativeActionPackets = saved.NativeActionPackets with { Headers = saved.NativeActionPackets.Headers.SetItem(0, retainedHeader with { Payload = retainedHeader.Payload.SetItem(17, 0xA7), UnknownByteMask = retainedHeader.UnknownByteMask & ~(1u << 17) }) } };
        NativeBattleCommandBanks banks = NativeBattleCommandBanks.Initialize();
        (banks, NativeBattleCommandLocation location) = banks.EnqueueCallback(0, 0, 7);
        BattleAttackCommand callback7 = new("party", "party", BattleAbility.EnemyAbility) { NativeCallback = new(0, 7) };
        banks = banks.BindCommand(location, 0, callback7);
        saved = saved with
        {
            Definition = saved.Definition with { NativeSavedBattleFlags = 8 },
            NativeSupport = saved.NativeSupport! with { SelectedAction = 8, GilgameshUsed = 1 },
            NativeCommandBanks = banks
        };
        BattleSession battle = BattleSession.Restore(saved);
        BattleSession independent = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(saved))!);
        battle.Tick();
        independent.Tick();
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(independent.Capture()));
        BattleAttackCommand prepared = Assert.Single(battle.QueuedActions, command => command.NativeQueuedCommand == 245);
        Assert.Equal(8, prepared.ActionId);
        Assert.NotNull(prepared.NativeHeaderIndex);
        Assert.Equal((byte)10, prepared.IndependentGuardian!.Level);
        Assert.Equal((ushort)330, prepared.IndependentGuardian.Action!.MagicId);
        Assert.Equal(saved.Combatants[0].NativeAtb, battle.Capture().Combatants[0].NativeAtb);
        Assert.Equal(saved.Combatants[0].Atb, battle.Combatants[0].Atb);
        Assert.Equal(9999, battle.Combatants.Single(actor => actor.Id == "enemy").CurrentHp);
        NativeBattleDescriptorPacket[] ordered = battle.NativeGraphics!.Descriptors.Queue.Select(index => battle.NativeGraphics.Descriptors.Packets[index]).ToArray();
        Assert.Equal(new ushort[] { 104, 10, 10 }, ordered.Select(packet => packet.Command));
        Assert.Equal(new uint[] { 0x47DD90, 0x47DDA0 }, ordered.Skip(1).Select(packet => packet.Argument));
        Assert.DoesNotContain(ordered, packet => packet.Command == 10 && packet.Argument == 0x47DE70);
        Assert.Equal(1u, battle.Capture().NativeClock!.ActionPause);
        NativeBattleActionHeaderSlot header = battle.Capture().NativeActionPackets!.Headers[prepared.NativeHeaderIndex!.Value];
        Assert.Equal((byte)245, header.ReadByte(1));
        Assert.Equal((ushort)8, NativeBattleActionGraphics.Word(header, 4));
        Assert.Equal((ushort)330, NativeBattleActionGraphics.Word(header, 6));
        Assert.Equal((byte)0, header.ReadByte(17));
    }
}
