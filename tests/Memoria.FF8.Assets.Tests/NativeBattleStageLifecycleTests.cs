using System.Text.Json;
using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleStageLifecycleTests
{
    [Theory]
    [InlineData(31)]
    [InlineData(51)]
    [InlineData(52)]
    [InlineData(107)]
    [InlineData(108)]
    [InlineData(136)]
    public void OriginalStageDescriptorLoadsTextureThenRegistersAndYieldsBeforeFirstPreCameraDraw(int stageId)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter formation = assets.LoadEncounter(94);
        BattleStage source = assets.LoadStage(stageId);
        BattleDefinition definition = CreateDefinition(game, assets, formation, source, stageId);
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
        Assert.False(NativeBattleStorageSnapshots.Capture(battle.NativeEffectMemory!.Textures!).VramRegions.IsEmpty);
        BattleSnapshot boundary = battle.Capture();
        BattleSession restored = BattleSession.Restore(NativeBattleSnapshotJson.Deserialize<BattleSnapshot>(NativeBattleSnapshotJson.Serialize(boundary))!);
        if (source.NativeSource!.Callback!.UpdateTexture0 == true)
        {
            Assert.Equal(source.NativeSource.TextureAnimations.ToArray(), battle.NativeStage.TextureAnimations!.Value.ToArray());
        }
        battle.Tick();
        restored.Tick();
        Assert.Equal(source.NativeSource!.RotationSpeed, battle.NativeStage.Rotation);
        if (source.NativeSource.Callback!.UpdateTexture0 == true)
        {
            Assert.Equal(NativeBattleStageTextureMotion.Advance50CB20(source.NativeSource.TextureAnimations, 0).Source.ToArray(), battle.NativeStage.TextureAnimations!.Value.ToArray());
            BattleSession frozenTexture = BattleSession.Restore(boundary with { NativeStage = boundary.NativeStage! with { Flags = boundary.NativeStage.Flags | 2 } });
            frozenTexture.Tick();
            Assert.Equal(source.NativeSource.TextureAnimations.ToArray(), frozenTexture.NativeStage!.TextureAnimations!.Value.ToArray());
            Assert.Equal(source.NativeSource.RotationSpeed, frozenTexture.NativeStage.Rotation);
        }
        Assert.Equal(NativeBattleSnapshotJson.Serialize(battle.Capture()), NativeBattleSnapshotJson.Serialize(restored.Capture()));
        for (int callback = 0; callback < 300 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        Assert.All(battle.NativeGraphics!.Models.Where(model => (model.Flags0 & 2) != 0), model =>
        {
            Assert.Equal(source.NativeSource!.ModelColors!.Color40, model.Color40);
            Assert.Equal(source.NativeSource.ModelColors.Color44, model.Color44);
        });
        BattleSnapshot completed = battle.Capture();
        NativeBattleRetainedMemory retained = NativeBattleRetainedMemory.Capture(completed, NativeFieldRuntimeProfile.FF8EnExecutableHash);
        Assert.Same(completed.NativeStage, retained.Stage);
        Assert.NotSame(battle.NativeStage, retained.Stage);
        BattleSession unknown = BattleSession.Restore(boundary with { NativeStage = null });
        Assert.Throws<NotSupportedException>(() => unknown.Tick());
    }

    [Theory]
    [InlineData(19)]
    [InlineData(37)]
    [InlineData(44)]
    [InlineData(45)]
    [InlineData(46)]
    [InlineData(92)]
    [InlineData(137)]
    [InlineData(147)]
    public void OriginalSpecializedStageRegistersDrawsAndResumesThroughEntryReadiness(int stageId)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter formation = assets.LoadEncounter(94);
        BattleStage source = assets.LoadStage(stageId);
        BattleDefinition definition = CreateDefinition(game, assets, formation, source, stageId);
        if (stageId == 147)
        {
            NativeBattleRetainedMemory retained147 = definition.NativeRetainedMemory!;
            NativeBattleStageState stage = retained147.Stage!;
            NativeBattleStageExtendedState extended = stage.Extended!;
            definition = definition with { NativeRetainedMemory = retained147 with { Stage = stage with { Extended = extended with { Stage147 = new() { Subphase = -1, Counter = 5 } } } } };
        }
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        Assert.Null(battle.NativeStage!.Task);
        for (int callback = 0; callback < 400 && battle.NativeStage.Task is null; callback++)
        {
            battle.Tick();
        }
        NativeBattleStageTask task = Assert.IsType<NativeBattleStageTask>(battle.NativeStage.Task);
        Assert.Equal(stageId == 147 ? new NativeBattleStageCallback(0x511EB0, null) : source.NativeSource!.Callback, task.Callback);
        Assert.Equal((short)1, task.Phase);
        if (stageId == 147)
        {
            Assert.Equal(new NativeBattleStage147State(), battle.NativeStage.Extended!.Stage147);
        }
        Assert.NotNull(battle.NativeStage.PaletteAllocation);
        Assert.False(NativeBattleStorageSnapshots.Capture(battle.NativeEffectMemory!.Textures!).VramRegions.IsEmpty);
        Assert.All(battle.NativeStage.Groups, group =>
        {
            Assert.NotNull(group.Source);
            Assert.False(group.Matrices.IsEmpty);
        });
        int rotationBeforeDraw = battle.NativeStage.Rotation;
        AssertResumesNextCallback(battle);
        Assert.Equal((battle.NativeStage.Flags & 1) == 0 ? unchecked(rotationBeforeDraw + battle.NativeStage.RotationSpeed) : rotationBeforeDraw, battle.NativeStage.Rotation);
        Assert.Equal((short)1, battle.NativeStage.Task!.Phase);
        Assert.NotEmpty(battle.NativeGraphics!.PrimitiveFrame!.LastCommands);
        for (int callback = 0; callback < 400 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        Assert.NotEmpty(battle.NativeGraphics!.PrimitiveFrame!.LastCommands);
        AssertResumesNextCallback(battle);
        BattleSnapshot completed = battle.Capture();
        NativeBattleRetainedMemory retained = NativeBattleRetainedMemory.Capture(completed, NativeFieldRuntimeProfile.FF8EnExecutableHash);
        Assert.Same(completed.NativeStage, retained.Stage);
        Assert.NotSame(battle.NativeStage, retained.Stage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void OriginalStage37OverlayServicesWritePacketsAndResumeTheirNextCallback(int service)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter formation = assets.LoadEncounter(94);
        BattleStage source = assets.LoadStage(37);
        BattleSession battle = new(CreateDefinition(game, assets, formation, source, 37), BattleAtbMode.TurnBased);
        for (int callback = 0; callback < 400 && battle.NativeStage!.Task is null; callback++)
        {
            battle.Tick();
        }
        Assert.Equal((short)1, battle.NativeStage!.Task!.Phase);
        NativeBattleStageExtendedState extended = battle.NativeStage.Extended!;
        NativeBattleStage37State state = extended.Stage37;
        if (service == 0)
        {
            state = NativeBattleStage37.UpdateHp518A80(state, 7, 8);
            state = NativeBattleStage37.Advance518F50(state, 1, battle.NativeStage.RotationSpeed, 0).State;
            Assert.Equal(5, state.FadeRemaining);
        }
        else
        {
            state = NativeBattleStage37.Dispatch518A80(state, service == 1 ? 100 : 102);
        }
        BattleSnapshot boundary = battle.Capture();
        battle = BattleSession.Restore(boundary with { NativeStage = boundary.NativeStage! with { Extended = extended with { Stage37 = state } } });
        short shake = battle.NativeCamera!.ScreenOffsetScratchY ?? 0;
        NativeBattleStage37Step expected = NativeBattleStage37.Advance518F50(state, 1, battle.NativeStage!.RotationSpeed, shake);
        Assert.Single(expected.Draws);
        AssertResumesNextCallback(battle);
        Assert.Equal(NativeBattleSnapshotJson.Serialize(expected.State), NativeBattleSnapshotJson.Serialize(battle.NativeStage!.Extended!.Stage37));
        if (service == 2)
        {
            Assert.Contains(battle.NativeGraphics!.PrimitiveFrame!.LastCommands, command => command.Command.Opcode == 0x66);
        }
        else
        {
            Assert.NotNull(battle.NativeStage.Extended.Stage37.ClutSource);
            AssertStage37OverlayPhysicalPackets(battle, expected.Draws[0]);
        }
    }

    private static void AssertStage37OverlayPhysicalPackets(BattleSession battle, NativeBattleStage37Draw draw)
    {
        NativeBattlePrimitiveProcess process = battle.NativeEffectMemory!.PrimitiveProcess!;
        NativeBattleLogicalOrdering ordering = battle.NativeGraphics!.PrimitiveFrame!.Pending;
        int[] offsets = [0, 12, 24, 40, 52, 64];
        NativeBattleLogicalProxy[] block = Enumerable.Range(0, Math.Max(0, ordering.Proxies.Length - 5)).Select(index => ordering.Proxies.Skip(index).Take(6).ToArray()).First(proxies => proxies.Select((proxy, index) => proxy.Flags20 == 0 && proxy.Packet.Bank == proxies[0].Packet.Bank && proxy.Packet.Buffer == proxies[0].Packet.Buffer && proxy.Packet.ByteOffset == proxies[0].Packet.ByteOffset + offsets[index]).All(matches => matches));
        NativeBattlePrimitiveProcess selected = process with { PacketBank = block[0].Packet.Bank, PacketBuffer = block[0].Packet.Buffer };
        byte[] rectangle = selected.ReadCommonPacket(block[2].Packet.ByteOffset, 16);
        Assert.Equal((byte)3, rectangle[3]);
        Assert.Equal((byte)0x62, rectangle[7]);
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(rectangle.AsSpan(8)));
        Assert.Equal(255u | (unchecked((uint)(ushort)draw.Height) << 16), BinaryPrimitives.ReadUInt32LittleEndian(rectangle.AsSpan(12)));
        byte[] mode = selected.ReadCommonPacket(block[5].Packet.ByteOffset, 8);
        Assert.Equal((byte)1, mode[3]);
        Assert.Equal(0xE1000600u | (unchecked((uint)draw.DrawMode) & 0x9FF), BinaryPrimitives.ReadUInt32LittleEndian(mode.AsSpan(4)));
        Assert.Equal(new uint[] { 0xE303C000, 0xE403FCFF }, Enumerable.Range(0, 2).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(selected.ReadCommonPacket(block[3].Packet.ByteOffset + 4 + index * 4, 4))).ToArray());
    }

    private static void AssertResumesNextCallback(BattleSession battle)
    {
        BattleSnapshot snapshot = battle.Capture();
        string serialized = NativeBattleSnapshotJson.Serialize(snapshot);
        BattleSession restored = BattleSession.Restore(NativeBattleSnapshotJson.Deserialize<BattleSnapshot>(serialized)!);
        Assert.Equal(serialized, NativeBattleSnapshotJson.Serialize(restored.Capture()));
        battle.Tick();
        restored.Tick();
        Assert.Equal(NativeBattleSnapshotJson.Serialize(battle.Capture()), NativeBattleSnapshotJson.Serialize(restored.Capture()));
    }

    private static BattleDefinition CreateDefinition(GameAssets game, BattleAssets assets, BattleEncounter formation, BattleStage source, int stageId)
    {
        BattleCombatantDefinition[] actors = [new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = 0, Speed = 1, InitialAtb = BattleSession.AtbMaximum }, new() { Id = "enemy", Name = "Ifrit", Information246 = assets.ReadEnemyNativeInformation246(61), ModelId = 61, SceneSlot = formation.Enemies.Single(enemy => enemy.ModelId == 61).Slot, Speed = 1, InitialAtb = 0 }];
        NativeBattleMusicEntryPreparation musicEntry = NativeBattleMusicFixture.Prepare(game, formation.Id);
        return new()
        {
            EncounterId = formation.Id,
            StageId = stageId,
            NativeFlags = 0x80,
            NativeEntry = new(0, 0),
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeMusicProcess = musicEntry.MusicProcess,
            NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5),
            NativeGlobalSoundProcess = musicEntry.SoundProcess,
            NativeGlobalRandom = new(0),
            NativeCrtRandom = NativeCrtRandomState.FromColdProcess(),
            NativeSavedOptions = game.ReadNewGameInputConfiguration().Options,
            NativeInputButtonMap = game.ReadNewGameInputConfiguration().ButtonMap,
            NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), formation),
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeStageSource = source.NativeSource,
            NativeStageCameraBank = source.NativeCameraBank,
            NativeStageCameraControl = source.NativeCameraControl,
            NativeStageCameraSelectors = [formation.PrimaryCamera, formation.AlternativeCamera],
            Combatants = actors
        };
    }
}
