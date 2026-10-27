using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshMasamuneProjectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Flare_projection_publishes_actual_geometry_and_preserves_packed_padding_knownness(bool unknownW)
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleModulePoolTask task = Task();
        if (unknownW)
        {
            task = task.MarkPayloadUnknown(0x16, 2);
        }
        NativeBattleEffectMemory restoredMemory = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(memory));
        NativeBattleModulePoolTask restoredTask = NativeBattleTestSnapshots.RestoreJson<NativeBattleModulePoolTask>(NativeBattleTestSnapshots.Json(task));
        NativeBattleGilgameshMasamuneProjectionStep step = NativeBattleGilgameshMasamuneProjection.Project(memory, task, Projection(), 0x2468);
        Assert.Equal((ushort)920, step.Task.ReadWord(0x10));
        Assert.Equal((ushort)588, step.Task.ReadWord(0x12));
        Assert.Equal((ushort)256, step.Task.ReadWord(0x14));
        Assert.Equal(NativeBattleStorageSnapshots.Capture(task).Payload.AsSpan().Slice(10, 2).ToArray(), NativeBattleStorageSnapshots.Capture(step.Task).Payload.AsSpan().Slice(10, 2).ToArray());
        Assert.Equal(task.Next, step.Task.Next);
        Assert.Equal(NativeBattleTestSnapshots.Json(NativeBattleStorageSnapshots.Capture(task).UnknownPayloadRanges), NativeBattleTestSnapshots.Json(NativeBattleStorageSnapshots.Capture(step.Task).UnknownPayloadRanges));
        NativeBattleGeometryRegisters geometry = step.Memory.GeometryRegisters!;
        Assert.Equal(new uint?[] { 22, 33, 0x03800500 }, geometry.Sxy);
        Assert.Equal(new uint?[] { 2, 3, 4, 256 }, geometry.Sz);
        Assert.Equal((uint?)0, geometry.Mac0);
        Assert.Equal((uint?)99, geometry.Otz);
        Assert.Equal((byte?)0x68, geometry.Rotation20[18]);
        Assert.Equal((byte?)0x24, geometry.Rotation20[19]);
        Assert.Equal(new int?[] { 0, 0, 0 }, geometry.Translation);
        NativeBattleScriptedColorRegisters colors = step.Memory.ScriptedColorRegisters!;
        Assert.Equal(new int?[] { 0, 0, 256 }, colors.Mac);
        Assert.Equal(new int?[] { 0, 0, 256 }, colors.Ir);
        Assert.Equal((uint?)0, colors.Ir0);
        Assert.Equal((uint?)0, colors.Flags);
        Assert.Equal((uint?)0x12345678, colors.Material);
        Assert.Equal(memory.ScriptedColorRegisters!.Rgb, colors.Rgb);
        Assert.Equal(!unknownW, colors.NormalInputKnown!.Value[6]);
        Assert.Equal(!unknownW, colors.NormalInputKnown.Value[7]);
        Assert.Equal(NativeBattleTestSnapshots.Json(step), NativeBattleTestSnapshots.Json(NativeBattleGilgameshMasamuneProjection.Project(restoredMemory, restoredTask, Projection(), 0x2468)));
    }

    [Fact]
    public void Unknown_projection_or_source_coordinate_is_not_replaced_with_a_default()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleGilgameshMasamuneProjection.Project(Memory(), Task(), Projection() with { FocalLength = null }, null));
        Assert.Throws<NotSupportedException>(() => NativeBattleGilgameshMasamuneProjection.Project(Memory(), Task().MarkPayloadUnknown(0x10, 2), Projection(), null));
    }

    [Fact]
    public void Missing_register_containers_publish_only_native_written_lanes_and_retain_every_other_lane_unknown()
    {
        NativeBattleEffectMemory memory = Memory() with { GeometryRegisters = null, ScriptedColorRegisters = null };
        NativeBattleGilgameshMasamuneProjectionStep result = NativeBattleGilgameshMasamuneProjection.Project(memory, Task().MarkPayloadUnknown(0x16, 2), Projection(), null);
        NativeBattleGeometryRegisters geometry = result.Memory.GeometryRegisters!;
        NativeBattleScriptedColorRegisters colors = result.Memory.ScriptedColorRegisters!;
        Assert.Equal(new uint?[] { null, null, 0x03800500 }, geometry.Sxy);
        Assert.Equal(new uint?[] { null, null, null, 256 }, geometry.Sz);
        Assert.Null(geometry.Otz);
        Assert.Equal(0u, geometry.Mac0);
        Assert.All(geometry.V1, value => Assert.Null(value));
        Assert.All(geometry.V2, value => Assert.Null(value));
        Assert.All(geometry.AnchorScratch8, value => Assert.Null(value));
        Assert.Null(geometry.Rotation20[18]);
        Assert.Null(geometry.Rotation20[19]);
        Assert.Null(colors.Material);
        Assert.All(colors.Rgb, value => Assert.Null(value));
        Assert.Equal(new int?[] { 0, 0, 256 }, colors.Mac);
        Assert.Equal(new int?[] { 0, 0, 256 }, colors.Ir);
        Assert.Equal(0u, colors.Ir0);
        Assert.Equal(0u, colors.Flags);
        Assert.Equal(new bool[] { true, true, true, true, true, true, false, false }, colors.NormalInputKnown!.Value);
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(memory))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattleGilgameshMasamuneProjection.Project(restored, Task().MarkPayloadUnknown(0x16, 2), Projection(), null)));
        Assert.Throws<NotSupportedException>(() => NativeBattleGilgameshMasamuneProjection.Project(memory, Task().MarkPayloadUnknown(0x10, 2), Projection(), null));
        Assert.Throws<NotSupportedException>(() => NativeBattleGilgameshMasamuneProjection.Project(memory, Task(), Projection() with { FocalLength = null }, null));
    }

    private static NativeBattleEffectMemory Memory()
    {
        return NativeBattleEffectMemory.FromColdProcess() with
        {
            GeometryRegisters = NativeBattleGeometryRegisters.Unknown() with { Sxy = [11, 22, 33], Sz = [1, 2, 3, 4], Otz = 99 },
            ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() with { Material = 0x12345678, Rgb = [1, 2, 3] }
        };
    }

    private static NativeBattleModulePoolTask Task() => new NativeBattleModulePoolTask(1, 0, 5, 0x590790, new byte[24].ToImmutableArray()).WriteWord(0x10, 0).WriteWord(0x12, 0).WriteWord(0x14, 256).WriteWord(0x16, 0x4321);
    private static NativeBattleCameraProjection Projection() => new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096), 256, 160, 112);
}
