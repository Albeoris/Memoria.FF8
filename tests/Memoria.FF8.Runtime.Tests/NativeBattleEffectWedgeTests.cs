using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectWedgeTests
{
    [Fact]
    public void Life_physical_hierarchy_uses_the_shared_three_draw_initialization_and_original_packet_extents()
    {
        var fixture = Fixture();
        List<NativeBattleModulePacketBatch> packets = [];
        NativeBattleEffectWedgeStep step = Advance(fixture.Frame, fixture.Address, fixture.Projection, fixture.Random, packets);
        NativeCrtRandomState expected = fixture.Random.Draw(out ushort yaw).Draw(out ushort pitch).Draw(out ushort roll);
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(step.Frame.Pools).Read(fixture.Address);
        Assert.Equal(expected, step.CrtRandomAfter);
        Assert.Equal((ushort)((yaw + 64) & 0xFFF), task.ReadWord(0x32));
        Assert.Equal((ushort)(pitch & 0xFFF), task.ReadWord(0x30));
        Assert.Equal((ushort)(roll & 0xFFF), task.ReadWord(0x34));
        Assert.Equal((ushort)175, task.ReadWord(0x1E));
        Assert.Equal((ushort)100, task.ReadWord(0x1C));
        Assert.Equal((ushort)300, task.ReadWord(0x20));
        Assert.Equal((ushort)8, task.ReadWord(0x54));
        Assert.Equal((ushort)0xA55A, task.ReadWord(0x5A));
        Assert.Equal((ushort)1, task.ReadWord(0x24));
        Assert.Equal((byte)1, task.ReadByte(0x29));
        Assert.Equal((byte)1, new NativeBattleEffectHierarchy(step.Frame.Pools).Read(fixture.Parent).ReadByte(0x28));
        NativeBattleModulePacketBatch batch = Assert.Single(packets);
        Assert.Equal(76, batch.AllocationBytes);
        Assert.Equal(new[] { 0, 28, 64 }, batch.Primitives.Select(primitive => primitive.ByteOffset).ToArray());
        Assert.Equal(new uint[] { 0x02000000, 0xE100002A, 0 }, batch.Primitives[2].Primitive.Words.ToArray());
        Assert.Equal((ushort)0, new NativeBattleEffectHierarchy(fixture.Frame.Pools).Read(fixture.Address).ReadWord(0x24));
    }

    [Fact]
    public void Immediate_packet_feedback_keeps_memory_and_task_writes_before_final_age_wrap_and_parent_completion()
    {
        var fixture = Fixture();
        int executions = 0;
        NativeBattleEffectWedgeStep step = NativeBattleEffectWedge.Advance8DCC20(fixture.Frame, fixture.Address, fixture.Random, ReadModel, fixture.Projection, (current, batch) =>
        {
            Assert.Equal(76, batch.AllocationBytes);
            Assert.Equal(0, executions++);
            NativeBattleEffectHierarchy hierarchy = new(current.Pools);
            NativeBattleModulePoolTask task = hierarchy.Read(fixture.Address).WriteWord(0x24, ushort.MaxValue).WriteByte(0x26, 5).WriteByte(0x2F, 0x9A);
            hierarchy = hierarchy.Write(fixture.Address, task);
            return current with { Pools = hierarchy.Pools, Memory = current.Memory with { Textures = current.Memory.Textures!.EnqueueCopy505EB0(1, 2, 3, 4, 5, 6) } };
        });
        Assert.Equal(1, executions);
        Assert.Equal(2, step.Result);
        NativeBattleEffectHierarchy after = new(step.Frame.Pools);
        NativeBattleModulePoolTask actual = after.Read(fixture.Address);
        Assert.Equal((ushort)0, actual.ReadWord(0x24));
        Assert.Equal((byte)0x9A, actual.ReadByte(0x2F));
        Assert.Equal((byte)0, after.Read(fixture.Parent).ReadByte(0x28));
        Assert.Equal(1, step.Frame.Memory.Textures!.Count);
        Assert.Equal(new NativeBattleEffectVramCopy(1, 2, 3, 4, 5, 6), step.Frame.Memory.Textures.Slots[0]!.Copy);
        Assert.Equal(0, fixture.Frame.Memory.Textures!.Count);
        Assert.Equal((byte)1, new NativeBattleEffectHierarchy(fixture.Frame.Pools).Read(fixture.Parent).ReadByte(0x28));
    }

    [Fact]
    public void Independent_frame_json_continuation_completes_the_exact_seventeen_callbacks_without_a_spell_frame_or_extra_random_draws()
    {
        var fixture = Fixture();
        NativeBattleEffectWedgeFrame frame = fixture.Frame;
        NativeCrtRandomState random = fixture.Random;
        int emitted = 0;
        for (int callback = 0; callback < 17; callback++)
        {
            NativeBattleEffectWedgeFrame independent = JsonSerializer.Deserialize<NativeBattleEffectWedgeFrame>(JsonSerializer.Serialize(frame))!;
            List<NativeBattleModulePacketBatch> packets = [];
            List<NativeBattleModulePacketBatch> restoredPackets = [];
            NativeBattleEffectWedgeStep actual = Advance(frame, fixture.Address, fixture.Projection, random, packets);
            NativeBattleEffectWedgeStep restored = Advance(independent, fixture.Address, fixture.Projection, random, restoredPackets);
            Assert.Equal(JsonSerializer.Serialize(actual), JsonSerializer.Serialize(restored));
            Assert.Equal(JsonSerializer.Serialize(packets), JsonSerializer.Serialize(restoredPackets));
            Assert.Equal(callback == 16 ? 2 : 0, actual.Result);
            emitted += packets.Count;
            frame = actual.Frame;
            random = actual.CrtRandomAfter;
        }
        Assert.Equal(16, emitted);
        Assert.Equal(fixture.Random.Draw(out _).Draw(out _).Draw(out _), random);
        NativeBattleEffectHierarchy hierarchy = new(frame.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(fixture.Address);
        Assert.Equal((ushort)1536, task.ReadWord(0x38));
        Assert.Equal((ushort)0, task.ReadWord(0x3A));
        Assert.Equal((ushort)17, task.ReadWord(0x24));
        Assert.Equal((ushort)0xA55A, task.ReadWord(0x5A));
        Assert.Equal((byte)0, hierarchy.Read(fixture.Parent).ReadByte(0x28));
        Assert.Equal(new ushort[] { 100, 100, 48, 112, 92 }, frame.Pools.Select(pool => pool.Stride).ToArray());
        Assert.Equal(new[] { 2, 4, 300, 300, 100 }, frame.Pools.Select(pool => pool.Slots.Length).ToArray());
    }

    private static NativeBattleEffectWedgeStep Advance(NativeBattleEffectWedgeFrame frame, uint address, NativeBattleCameraProjection projection, NativeCrtRandomState random, List<NativeBattleModulePacketBatch> packets)
    {
        return NativeBattleEffectWedge.Advance8DCC20(frame, address, random, ReadModel, projection, (current, batch) =>
        {
            packets.Add(batch);
            return current;
        });
    }

    private static NativeBattleModelTransform ReadModel(byte target)
    {
        Assert.Equal((byte)2, target);
        return new(new(0, 200, 0), new(0, 200, 0), 0, 0, 4096);
    }

    private static (NativeBattleEffectWedgeFrame Frame, uint Address, uint Parent, NativeBattleCameraProjection Projection, NativeCrtRandomState Random) Fixture()
    {
        //Actual Life8649C0 descriptors, banks and dimensions; the shared callback receives no spell identity or capacity defaults.
        NativeBattleEffectHierarchy hierarchy = new([
            NativeBattleEffectTaskPool.Initialize(0x26AC868, 0x26AC7A0, 100, 2),
            NativeBattleEffectTaskPool.Initialize(0x26B4BD0, 0x26B6FD0, 100, 4),
            NativeBattleEffectTaskPool.Initialize(0x26B7178, 0x26B7188, 48, 300),
            NativeBattleEffectTaskPool.Initialize(0x26B4BB8, 0x26AC878, 112, 300),
            NativeBattleEffectTaskPool.Initialize(0x26B7160, 0x26B4BE0, 92, 100)
        ]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x864AB0, 100);
        NativeBattleEffectHierarchyAppend parent = root.Hierarchy.Append8DC540(1, 0x864BC0, 100, root.Address, (_, _, _) => new(0, 2));
        hierarchy = parent.Hierarchy;
        hierarchy = hierarchy.Write(parent.Address, hierarchy.Read(parent.Address).WriteWord(0x38, 100).WriteWord(0x3C, 300).WriteWord(0x4A, unchecked((ushort)-100)).WriteWord(0x52, 50));
        NativeBattleEffectHierarchyAppend wedge = hierarchy.Append8DC540(4, 0x8DCC20, 92, parent.Address, (_, _, _) => new(0, 2));
        hierarchy = wedge.Hierarchy;
        NativeBattleModulePoolTask task = hierarchy.Read(wedge.Address).WriteWord(0x56, 8).WriteByte(0x4C, 16).WriteByte(0x4D, 16).WriteByte(0x4E, 16).WriteByte(0x52, 16).WriteWord(0x3C, 256).WriteWord(0x3E, 16).WriteWord(0x40, 64).WriteWord(0x42, 16).WriteWord(0x58, 64).WriteWord(0x5A, 0xA55A);
        hierarchy = hierarchy.Write(wedge.Address, task);
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 20000, 4096);
        return (new(NativeBattleEffectMemory.FromColdProcess(), hierarchy.Pools), wedge.Address, parent.Address, new(camera, 216, 160, 108), NativeCrtRandomState.FromColdProcess());
    }
}
