using System.Text.Json;
using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleStageLifecycleTests
{
    [Fact]
    public void OriginalStageDescriptorLoadsTextureThenRegistersAndYieldsBeforeFirstPreCameraDraw()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter formation = assets.LoadEncounter(94);
        BattleStage source = assets.LoadStage(formation.StageId);
        BattleCombatantDefinition[] actors = [new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = 0 }, new() { Id = "enemy", Name = "Ifrit", Information246 = assets.ReadEnemyNativeInformation246(61), ModelId = 61, SceneSlot = formation.Enemies.Single(enemy => enemy.ModelId == 61).Slot }];
        NativeBattleMusicEntryPreparation musicEntry = NativeBattleMusicFixture.Prepare(game, formation.Id);
        BattleDefinition definition = new()
        {
            EncounterId = formation.Id,
            StageId = formation.StageId,
            NativeFlags = 0x80,
            NativeEntry = new(0, 0),
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeMusicProcess = musicEntry.MusicProcess,
            NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5),
            NativeGlobalSoundProcess = musicEntry.SoundProcess,
            NativeGlobalRandom = new(0),
            NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), formation),
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeStageSource = source.NativeSource,
            NativeStageCameraBank = source.NativeCameraBank,
            NativeStageCameraControl = source.NativeCameraControl,
            NativeStageCameraSelectors = [formation.PrimaryCamera, formation.AlternativeCamera],
            Combatants = actors
        };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        Assert.Null(battle.NativeStage!.Task);
        for (int callback = 0; callback < 200 && battle.NativeStage.PaletteAllocation is null; callback++)
        {
            battle.Tick();
        }
        NativeBattleStagePaletteAllocation palette = Assert.IsType<NativeBattleStagePaletteAllocation>(battle.NativeStage.PaletteAllocation);
        NativeBattlePrimitiveProcess primitive = battle.NativeEffectMemory!.PrimitiveProcess!;
        Assert.Equal(0, palette.Address.ByteOffset);
        Assert.Equal(palette.Address.Bank, primitive.PacketBank);
        Assert.Equal(palette.Address.Buffer, primitive.PacketBuffer);
        //500900 resets the selected primitive cursor later in this same Tick; the pure506CF0 test checks its512-byte advance before reset.
        Assert.Equal(0, primitive.ByteCursor);
        Assert.Equal(palette.Source.Data.ToArray(), battle.NativeEffectMemory.Textures!.ReadVram().AsSpan(239 * 1024 * 2, 512).ToArray());
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(palette.Source.Data.AsSpan()));
        for (int color = 1; color < 256; color++)
        {
            Assert.Equal((ushort)0xC210, BinaryPrimitives.ReadUInt16LittleEndian(palette.Source.Data.AsSpan().Slice(2 * color)));
        }
        Assert.DoesNotContain(battle.NativeGraphics!.PrimitiveFrame!.Pending.Packets, packet => packet.Reference.Bank == palette.Address.Bank && packet.Reference.Buffer == palette.Address.Buffer && packet.Reference.ByteOffset < palette.Address.ByteOffset + 512 && packet.Reference.ByteOffset + packet.Words.Length * 4 > palette.Address.ByteOffset);
        for (int callback = 0; callback < 200 && battle.NativeStage.Task is null; callback++)
        {
            battle.Tick();
        }
        Assert.NotNull(battle.NativeStage.Task);
        Assert.Equal((short)1, battle.NativeStage.Task!.Phase);
        Assert.Equal(0, battle.NativeStage.Rotation);
        Assert.All(battle.NativeStage.Groups, group => Assert.Equal((byte)1, group.Counter));
        Assert.False(battle.NativeEffectMemory!.Textures!.VramRegions.IsEmpty);
        BattleSnapshot boundary = battle.Capture();
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(boundary))!);
        battle.Tick();
        restored.Tick();
        Assert.Equal(16, battle.NativeStage.Rotation);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        for (int callback = 0; callback < 300 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        Assert.All(battle.NativeGraphics!.Models.Where(model => (model.Flags0 & 2) != 0), model =>
        {
            Assert.Equal((uint?)0x00464B6E, model.Color40);
            Assert.Equal((uint?)0x32464646, model.Color44);
        });
        NativeBattleRetainedMemory retained = NativeBattleRetainedMemory.Capture(battle.Capture(), NativeFieldRuntimeProfile.FF8EnExecutableHash);
        Assert.Same(battle.NativeStage, retained.Stage);
        BattleSession unknown = BattleSession.Restore(boundary with { NativeStage = null });
        Assert.Throws<NotSupportedException>(() => unknown.Tick());
    }
}
