using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect76StageReloadTests
{
    [Fact]
    public void Index1_uses_its_loaded_directory_and_retained_current_destination()
    {
        byte[] directory = new byte[32];
        BinaryPrimitives.WriteUInt32LittleEndian(directory, 6);
        BinaryPrimitives.WriteUInt32LittleEndian(directory.AsSpan(24), 0x1200);
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess() with { SourceAddress1D9A820 = 0x500000 };
        List<(uint, int)> reads = [];
        NativeBattleStageReloadCopy plan = NativeBattleStageReload512AC0.Plan(stage, 1, 0x2100000, (address, length) =>
        {
            reads.Add((address, length));
            return directory;
        });
        Assert.Equal(new NativeBattleStageReloadCopy(0x210137C, 0x500000, 0x3220, 0x210257C, 0x501214), plan);
        Assert.Equal((0x210137Cu, 32), Assert.Single(reads));
        Assert.Equal((short)38, NativeBattleStageReload512AC0.File512AA0(1));
        Assert.Throws<NotSupportedException>(() => NativeBattleStageReload512AC0.File512AA0(2));
    }

    [Fact]
    public void Index1_reads_its_distinct_warm_seed_and_retains_stage_lifecycle_after_json()
    {
        NativeBattleStageSource source = Source();
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess().Initialize500EA0(source) with
        {
            SourceAddress1D9A820 = 0x500000,
            ReloadColorSeed1D9A824 = 0xDE112233,
            ReloadColorSeed1D9A828 = 0xAB654321,
            Flags = 0x87654321,
            Rotation = 0x12345678,
            Task = new(new(0x512500, false), 7)
        };
        NativeBattleStageState restored = JsonSerializer.Deserialize<NativeBattleStageState>(JsonSerializer.Serialize(stage))!;
        NativeBattleStageState result = restored.Reload512AC0(1, source with { Groups = [source.Groups[0], null, source.Groups[2], source.Groups[3]] });
        Assert.Equal(stage.Flags, result.Flags);
        Assert.Equal(stage.Rotation, result.Rotation);
        Assert.Equal(stage.Task, result.Task);
        Assert.Equal(stage.SourceAddress1D9A820, result.SourceAddress1D9A820);
        Assert.Equal(stage.ReloadColorSeed1D9A824, result.ReloadColorSeed1D9A824);
        Assert.Equal(stage.ReloadColorSeed1D9A828, result.ReloadColorSeed1D9A828);
        Assert.Equal(new NativeBattleStageColors(0x006C6C6C, 0x32654321), result.ModelColors);
        Assert.Equal((short)2, result.RotationSpeed);
        Assert.Equal((ushort)2, result.FinalStage142!.SavedRotation);
        Assert.Equal((byte)7, (byte)(result.Groups[0].Flags & 7));
        Assert.Equal((byte)0, (byte)(result.Groups[1].Flags & 7));
        Assert.Equal(stage.FinalStage142! with { SavedRotation = 2 }, result.FinalStage142);
    }

    [Fact]
    public void Missing_imported_seed_stays_unknown_and_cannot_borrow_the_other_row()
    {
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess() with { SourceAddress1D9A820 = 0x500000, ReloadColorSeed1D9A828 = null };
        NativeBattleStageState restored = JsonSerializer.Deserialize<NativeBattleStageState>(JsonSerializer.Serialize(stage))!;
        Assert.Null(restored.ReloadColorSeed1D9A828);
        Assert.Equal(0u, restored.ReloadColorSeed1D9A824);
        Assert.Throws<NotSupportedException>(() => restored.Reload512AC0(1, Source()));
        Assert.Equal(0u, NativeBattleStageState.FromColdProcess().ReloadColorSeed1D9A828);
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
