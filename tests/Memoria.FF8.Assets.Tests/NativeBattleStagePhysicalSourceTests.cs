using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleStagePhysicalSourceTests
{
    [Fact]
    public void Original_stage140_geometry_stays_in_the_prefix_reserved_before_enemy_heap()
    {
        using BattleAssets assets = Open();
        NativeBattleStageSource source = assets.LoadStage(140).NativeSource!;
        NativeBattleStagePhysicalSource physical = source.PhysicalSource!;
        physical.Validate();
        Assert.Equal(0x1939018u, physical.Address);
        Assert.Equal(0xB74, physical.DirectoryOffset);
        Assert.Equal(0x4114, physical.Prefix.Length);
        uint[] addresses = [0x1939C30, 0x193AEF8, 0x193B918, 0x193C3C8];
        int[] lengths = [4660, 2444, 2588, 3412];
        int[] objects = [6, 1, 4, 2];
        (NativeBattleFileStaging staging, NativeBattleModelCpuResources cpu) = physical.Publish500EA0(NativeBattleFileStaging.Unknown(), NativeBattleModelCpuResources.FromColdProcess() with { ScratchMaximum = 0, EnemyCursor = 0 });
        Assert.Equal(0x4114, cpu.EnemyCursor);
        for (int group = 0; group < 4; group++)
        {
            Assert.Equal(addresses[group], physical.GroupGeometryAddress(group));
            Assert.Equal(lengths[group], physical.GroupGeometryLength(group));
            Assert.Equal(objects[group], BinaryPrimitives.ReadInt32LittleEndian(staging.Read(addresses[group], 4)));
            Assert.Equal(physical.Prefix.AsSpan().Slice(checked((int)(addresses[group] - physical.Address)), lengths[group]).ToArray(), staging.Read(addresses[group], lengths[group]));
        }
        Assert.True(cpu.ScratchMaximum > 0);
    }

    [Fact]
    public void Physical_stage_source_survives_JSON_and_an_absent_old_owner_stays_absent()
    {
        using BattleAssets assets = Open();
        NativeBattleStageSource source = assets.LoadStage(140).NativeSource!;
        string json = JsonSerializer.Serialize(source);
        NativeBattleStageSource restored = JsonSerializer.Deserialize<NativeBattleStageSource>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        restored.PhysicalSource!.Validate();
        Assert.Equal(source.PhysicalSource!.Prefix.ToArray(), restored.PhysicalSource.Prefix.ToArray());
        NativeBattleStageSource old = source with { PhysicalSource = null };
        string oldJson = JsonSerializer.Serialize(old);
        Assert.Null(JsonSerializer.Deserialize<NativeBattleStageSource>(oldJson)!.PhysicalSource);
    }

    private static BattleAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources for physical stage ingress.");
        return new(directory!);
    }
}
