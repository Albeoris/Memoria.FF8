using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleBoneParticleTests
{
    [Fact]
    public void PublicEntryB1UsesTheSharedPoolAndRestoresItsLiveProgramBeforeTheParticleThreshold()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter formation = assets.LoadEncounter(94);
        BattleCombatantDefinition[] actors = [new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = 0 }, new() { Id = "enemy", Name = "Ifrit", Information246 = assets.ReadEnemyNativeInformation246(61), ModelId = 61, SceneSlot = 0, Speed = 1 }];
        NativeBattleGraphicsDefinition graphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), formation);
        byte[] program = new byte[29];
        BinaryPrimitives.WriteUInt16LittleEndian(program, 8);
        BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(2), 18);
        BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(16), 22);
        Convert.FromHexString("A300E6FFB1F081FF00AAA2").CopyTo(program, 18);
        NativeBattleGraphicsBinding binding = graphics.Bindings.Single(binding => binding.ActorId == "party");
        graphics = graphics with { Bindings = graphics.Bindings.SetItem(graphics.Bindings.IndexOf(binding), binding with { Resource = binding.Resource with { SequenceSection = program.ToImmutableArray() } }) };
        NativeBattleMusicEntryPreparation musicEntry = NativeBattleMusicFixture.Prepare(game, formation.Id);
        BattleSession battle = new(new() { StageId = formation.StageId, NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(), NativeMusicProcess = musicEntry.MusicProcess, NativeGlobalSoundProcess = musicEntry.SoundProcess, NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5), NativeFlags = 0x80, NativeEntry = new(0, 0), NativeGlobalRandom = new(0), NativeStageSource = assets.LoadStage(formation.StageId).NativeSource, NativeStageCameraBank = assets.LoadStage(formation.StageId).NativeCameraBank, NativeStageCameraSelectors = [formation.PrimaryCamera, formation.AlternativeCamera], NativeStageCameraControl = assets.LoadStage(formation.StageId).NativeCameraControl, NativeGraphics = graphics, NativeModuleResources = game.ReadNativeBattleModuleResources(), Combatants = actors }, BattleAtbMode.TurnBased);
        for (int callback = 0; callback < 300 && !battle.NativeGraphics!.Auxiliary.Slots.Any(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.SoundParticle50F830); callback++)
        {
            battle.Tick();
        }
        BattleSnapshot pending = battle.Capture();
        NativeBattleAuxiliaryTask task = Assert.Single(pending.NativeGraphics!.Auxiliary.Slots, task => (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.SoundParticle50F830);
        Assert.Equal(24, task.Program!.Offset);
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan().Slice(8)));
        Assert.Equal(0x81, task.Program.Source[task.Program.Offset]);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(pending))!);
        battle.Tick();
        restored.Tick();
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        Assert.DoesNotContain(battle.NativeGraphics!.Auxiliary.Slots, task => (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.SoundParticle50F830);
        NativeBattleModuleDraw draw = Assert.Single(battle.NativeGraphics.ModuleDraws, draw => draw.Kind == NativeBattleModuleDrawKind.Sprite);
        Assert.Equal((short)0, draw.Anchor!.Y);
        Assert.Equal((uint)3, draw.PackedParameters & 255);
        Assert.DoesNotContain(battle.Events, entry => entry.NativeModuleSound is { Sound: 2 or 3 });
    }

    [Fact]
    public void OriginalB1ResourceThreeUsesPlacementThreeAndClearsHeightBeforeItsActualSpriteCallback()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleModuleResource resource = game.ReadNativeBattleModuleResources().Single(resource => resource.Id == 3);
        Assert.Equal(0x570CC0u, resource.WrapperCallback);
        Assert.Equal(0x570BC0u, resource.Callback);
        NativeBattleModuleState state = NativeBattleModuleState.Clear().AppendBoneParticle(3, 240, short.MinValue, resource, 3u | 9u << 16);
        NativeBattleModuleState restored = JsonSerializer.Deserialize<NativeBattleModuleState>(JsonSerializer.Serialize(state))!;
        NativeBattleModuleInputs inputs = new(0) { Resources = [resource], ReadModelBone = (model, bone, fraction) =>
        {
            Assert.Equal(3, model);
            Assert.Equal((byte)240, bone);
            Assert.Equal((short)0, fraction);
            return new(10, 20, 30);
        } };
        NativeBattleModuleStep step = state.Advance(inputs);
        Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(restored.Advance(inputs)));
        NativeBattleModuleDraw draw = Assert.Single(step.Draws);
        Assert.Equal(NativeBattleModuleDrawKind.Sprite, draw.Kind);
        Assert.Equal(new NativeBattleModuleAnchor(10, 0, 30, 0), draw.Anchor);
        Assert.Equal(9 << 8, draw.Scale);
        Assert.Equal(3u | 9u << 16, draw.PackedParameters);
        Assert.Equal((ushort)0, step.State.Slots[0].Flags);
        Assert.Equal(NativeBattleModuleKind.Sprite570BC0, step.State.Slots[1].Kind);
    }
}
