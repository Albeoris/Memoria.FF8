using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect69StageReloadTests
{
    [Fact]
    public void Copy_plan_reads_loaded_directory_and_retains_current_destination()
    {
        byte[] directory = new byte[32];
        BinaryPrimitives.WriteInt32LittleEndian(directory, 6);
        BinaryPrimitives.WriteInt32LittleEndian(directory.AsSpan(24), 0x2DA4);
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess() with { SourceAddress1D9A820 = 0x193A998 };
        List<(uint, int)> reads = [];
        NativeBattleStageReloadCopy plan = NativeBattleStageReload512AC0.Plan(stage, 0, 0x20E7AD8, (address, length) =>
        {
            reads.Add((address, length));
            return directory;
        });
        Assert.Equal(new NativeBattleStageReloadCopy(0x20E953C, 0x193A998, 0x4DC4, 0x20EC2E0, 0x193D750), plan);
        Assert.Equal((0x20E953Cu, 32), Assert.Single(reads));
        Assert.Equal((short)37, NativeBattleStageReload512AC0.File512AA0(0));
        Assert.Throws<NotSupportedException>(() => NativeBattleStageReload512AC0.File512AA0(2));
    }

    [Fact]
    public void Reload_changes_selected_fields_without_reinitializing_stage_lifecycle()
    {
        NativeBattleStageSource source = Source();
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess().Initialize500EA0(source);
        stage = stage with
        {
            SourceAddress1D9A820 = 0x193A998,
            ReloadColorSeed1D9A824 = 0xAB123456,
            Flags = 0x87654321,
            Rotation = 0x12345678,
            Task = new(new(0x512500, false), 7),
            Groups = stage.Groups.Select(group => group with { Flags = 0xFB, ColorDepth = -123, ColorFlags = 0xDEADBEEF }).ToImmutableArray(),
            FinalStage142 = stage.FinalStage142! with { TargetRatio = 123, TransitionA = 456, SavedRotation = 99 }
        };
        NativeBattleStageState result = stage.Reload512AC0(0, source with { Groups = [source.Groups[0], null, source.Groups[2], source.Groups[3]] });
        Assert.Equal(stage.Flags, result.Flags);
        Assert.Equal(stage.Rotation, result.Rotation);
        Assert.Same(stage.Task, result.Task);
        Assert.Equal((short)8, result.RotationSpeed);
        Assert.Equal(stage.FinalStage142 with { SavedRotation = 8 }, result.FinalStage142);
        Assert.Equal(new NativeBattleStageColors(0x00606868, 0x32123456), result.ModelColors);
        Assert.Equal((byte)0xFF, result.Groups[0].Flags);
        Assert.Equal((byte)0xF8, result.Groups[1].Flags);
        Assert.Same(stage.Groups[1].Source, result.Groups[1].Source);
        Assert.All(result.Groups, group =>
        {
            Assert.Equal((short)-123, group.ColorDepth);
            Assert.Equal(0xDEADBEEFu, group.ColorFlags);
        });
        Assert.Equal(0x193A998u, result.SourceAddress1D9A820);
        Assert.Equal(0xAB123456u, result.ReloadColorSeed1D9A824);
    }

    [Fact]
    public void Missing_imported_source_or_seed_remains_unsupported_after_snapshot()
    {
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess() with { SourceAddress1D9A820 = null, ReloadColorSeed1D9A824 = null };
        NativeBattleStageState restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleStageState>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(stage)))!);
        Assert.Null(restored.SourceAddress1D9A820);
        Assert.Null(restored.ReloadColorSeed1D9A824);
        Assert.Throws<NotSupportedException>(() => restored.Reload512AC0(0, Source()));
        Assert.Throws<NotSupportedException>(() => (restored with { SourceAddress1D9A820 = 0x193A998 }).Reload512AC0(0, Source()));
        Assert.Equal(0u, NativeBattleStageState.FromColdProcess().ReloadColorSeed1D9A824);
    }

    [Fact]
    public void Current_geometry_address_reads_replaced_physical_directory()
    {
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess() with { SourceAddress1D9A820 = 0x193A998 };
        byte[] directory = new byte[32];
        BinaryPrimitives.WriteInt32LittleEndian(directory, 6);
        BinaryPrimitives.WriteInt32LittleEndian(directory.AsSpan(8), 0x15B0);
        BinaryPrimitives.WriteInt32LittleEndian(directory.AsSpan(12), 0x1CC8);
        byte[] group = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(group, 3);
        BinaryPrimitives.WriteInt32LittleEndian(group.AsSpan(8), 132);
        BinaryPrimitives.WriteInt32LittleEndian(group.AsSpan(12), 1800);
        Assert.Equal(0x193BFCCu, stage.CurrentGroupGeometryAddress(1, (address, length) => address == 0x193A998 ? directory : address == 0x193BF48 ? group : throw new InvalidDataException()));
    }

    private static NativeBattleStageSource Source()
    {
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        byte[] animations = new byte[17];
        BinaryPrimitives.WriteInt32LittleEndian(animations, 1);
        BinaryPrimitives.WriteInt32LittleEndian(animations.AsSpan(4), 8);
        animations[8] = 3;
        NativeBattlePoseSource pose = new(skeleton.ToImmutableArray(), animations.ToImmutableArray());
        return new(16, [pose, pose, pose, pose], [], new("stage:TIM", [])) { ModelColors = new(0x00808080, 0x32808080), Geometry = [] };
    }
}
