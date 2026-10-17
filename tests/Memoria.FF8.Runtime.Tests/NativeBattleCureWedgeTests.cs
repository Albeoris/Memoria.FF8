using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCureWedgeTests
{
    [Fact]
    public void Thin_cure_adapter_preserves_immediate_memory_effect_and_task_changes()
    {
        var fixture = Fixture();
        NativeBattleCureParticleStep step = NativeBattleCureParticles.AdvanceWedge8DCC20(fixture.Frame, fixture.Address, fixture.Inputs with
        {
            Execute = (current, operation) =>
            {
                Assert.IsType<NativeBattleCurePacketOperation>(operation);
                NativeBattleEffectHierarchy hierarchy = new(current.Effect.Pools);
                hierarchy = hierarchy.Write(fixture.Address, hierarchy.Read(fixture.Address).WriteWord(0x24, 99));
                return current with
                {
                    Memory = current.Memory with { Textures = current.Memory.Textures!.EnqueueCopy505EB0(1, 2, 3, 4, 5, 6) },
                    Effect = current.Effect with { Pools = hierarchy.Pools, DrawArenaOffset = 789 }
                };
            }
        });
        Assert.Equal(1, step.Frame.Memory.Textures!.Count);
        Assert.Equal(new NativeBattleEffectVramCopy(1, 2, 3, 4, 5, 6), step.Frame.Memory.Textures.Slots[0]!.Copy);
        Assert.Equal(789, step.Frame.Effect.DrawArenaOffset);
        Assert.Equal((ushort)100, new NativeBattleEffectHierarchy(step.Frame.Effect.Pools).Read(fixture.Address).ReadWord(0x24));
        Assert.Equal(0, fixture.Frame.Memory.Textures!.Count);
        Assert.Null(fixture.Frame.Effect.DrawArenaOffset);
    }

    [Fact]
    public void Initial_black_degenerate_polygons_still_publish_the_mode_packet_and_consume_three_random_draws_in_yaw_pitch_roll_order()
    {
        var fixture = Fixture();
        List<NativeBattleCureOperation> operations = [];
        NativeBattleCureParticleStep step = Advance(fixture.Frame, fixture.Address, fixture.Inputs, operations);
        NativeCrtRandomState expected = fixture.Inputs.CrtRandom.Draw(out ushort yaw).Draw(out ushort pitch).Draw(out ushort roll);
        Assert.Equal(expected, step.CrtRandomAfter);
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(step.Frame.Effect.Pools).Read(fixture.Address);
        Assert.Equal((ushort)((yaw + 64) & 0xFFF), task.ReadWord(0x32));
        Assert.Equal((ushort)(pitch & 0xFFF), task.ReadWord(0x30));
        Assert.Equal((ushort)(roll & 0xFFF), task.ReadWord(0x34));
        Assert.Equal((ushort)175, task.ReadWord(0x1E));
        Assert.Equal((ushort)100, task.ReadWord(0x1C));
        Assert.Equal((ushort)300, task.ReadWord(0x20));
        NativeBattleModulePacketBatch batch = Assert.IsType<NativeBattleCurePacketOperation>(Assert.Single(operations)).Batch;
        Assert.Equal(76, batch.AllocationBytes);
        Assert.Equal(new[] { 0, 28, 64 }, batch.Primitives.Select(primitive => primitive.ByteOffset).ToArray());
        Assert.All(batch.Primitives, primitive => Assert.Equal((ushort)0, primitive.Flags20));
        Assert.Equal(new uint[] { 0x02000000, 0xE100002A, 0 }, batch.Primitives[2].Primitive.Words.ToArray());
        Assert.Equal(new int[] { 0, 0, 0, 0 }, batch.Primitives[2].Primitive.VertexPayload4.ToArray());
        Assert.Equal(0x32000000u, batch.Primitives[0].Primitive.Words[1]);
        Assert.Equal(0x3A000000u, batch.Primitives[1].Primitive.Words[1]);
        Assert.Equal(batch.Primitives[0].Primitive.Words[2], batch.Primitives[0].Primitive.Words[4]);
    }

    [Fact]
    public void Native_packet_layout_retains_bright_dim_black_vertices_and_each_gte_depth_without_culling_the_wedge()
    {
        var fixture = Fixture();
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(fixture.Frame.Effect.Pools).Read(fixture.Address).WriteWord(0x38, 200).WriteWord(0x3A, 100).WriteWord(0x30, 0).WriteWord(0x32, 0).WriteWord(0x34, 0).WriteDword(0x44, 0x112233).WriteDword(0x48, 0x445566);
        NativeBattleModulePacketBatch batch = NativeBattleCureParticles.EmitWedge8DCC20(task, fixture.Inputs.Projection!);
        NativeBattleGeometryPrimitive triangle = batch.Primitives[0].Primitive;
        NativeBattleGeometryPrimitive quad = batch.Primitives[1].Primitive;
        Assert.Equal(0x32112233u, triangle.Words[1]);
        Assert.Equal(0x445566u, triangle.Words[3]);
        Assert.Equal(0x445566u, triangle.Words[5]);
        Assert.Equal(0x3A445566u, quad.Words[1]);
        Assert.Equal(0x445566u, quad.Words[3]);
        Assert.Equal(0u, quad.Words[5]);
        Assert.Equal(0u, quad.Words[7]);
        Assert.Equal(new[] { 20000, 20000, 20000, 0 }, triangle.VertexPayload4.ToArray());
        Assert.Equal(new[] { 20000, 20000, 20000, 20000 }, quad.VertexPayload4.ToArray());
        Assert.Equal(17 + ((60000 * 0x155 >> 12) >> 2), triangle.Bucket);
        Assert.All(batch.Primitives, primitive => Assert.Equal(triangle.Bucket, primitive.Primitive.Bucket));
        Assert.Equal(triangle.Words[4], quad.Words[2]);
        Assert.Equal(triangle.Words[6], quad.Words[4]);
    }

    [Fact]
    public void Grow_and_shrink_are_eight_callbacks_each_and_json_continuation_keeps_immediate_operations_and_parent_release()
    {
        var fixture = Fixture();
        NativeBattleCureFrame frame = fixture.Frame;
        NativeCrtRandomState random = fixture.Inputs.CrtRandom;
        int emitted = 0;
        NativeBattleCureParticleStep? last = null;
        for (int callback = 0; callback < 17; callback++)
        {
            NativeBattleCureFrame restored = JsonSerializer.Deserialize<NativeBattleCureFrame>(JsonSerializer.Serialize(frame))!;
            List<NativeBattleCureOperation> operations = [];
            NativeBattleCureParticleInputs inputs = fixture.Inputs with { CrtRandom = random };
            NativeBattleCureParticleStep step = Advance(frame, fixture.Address, inputs, operations);
            List<NativeBattleCureOperation> restoredOperations = [];
            NativeBattleCureParticleStep independent = Advance(restored, fixture.Address, inputs, restoredOperations);
            Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(independent));
            Assert.Equal(JsonSerializer.Serialize(operations.Cast<NativeBattleCurePacketOperation>()), JsonSerializer.Serialize(restoredOperations.Cast<NativeBattleCurePacketOperation>()));
            Assert.Equal(callback == 16 ? 2 : 0, step.Result);
            emitted += operations.Count;
            frame = step.Frame;
            random = step.CrtRandomAfter;
            last = step;
        }
        Assert.Equal(16, emitted);
        NativeBattleEffectHierarchy hierarchy = new(last!.Frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(fixture.Address);
        Assert.Equal((ushort)1536, task.ReadWord(0x38));
        Assert.Equal((ushort)0, task.ReadWord(0x3A));
        Assert.Equal((ushort)17, task.ReadWord(0x24));
        Assert.Equal((byte)3, task.ReadByte(0x29));
        Assert.Equal((byte)5, task.ReadByte(0x26));
        Assert.Equal((byte)0, hierarchy.Read(task.ReadDword(0x18)).ReadByte(0x28));
        Assert.Equal(fixture.Inputs.CrtRandom.Draw(out _).Draw(out _).Draw(out _), last.CrtRandomAfter);
    }

    [Fact]
    public void Disabled_final_callback_needs_no_projection_and_signed_short_timer_and_byte_colors_wrap()
    {
        var fixture = Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(fixture.Address).WriteByte(0x29, 2).WriteWord(0x54, 0).WriteByte(0x44, 4).WriteWord(0x38, 0);
        hierarchy = hierarchy.Write(fixture.Address, task);
        NativeBattleCureFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        List<NativeBattleCureOperation> operations = [];
        NativeBattleCureParticleStep step = Advance(frame, fixture.Address, fixture.Inputs with { Projection = null }, operations);
        Assert.Empty(operations);
        Assert.Equal(2, step.Result);
        task = new NativeBattleEffectHierarchy(step.Frame.Effect.Pools).Read(fixture.Address);
        Assert.Equal(ushort.MaxValue, task.ReadWord(0x54));
        Assert.Equal((byte)244, task.ReadByte(0x44));
        Assert.Equal((short)-64, unchecked((short)task.ReadWord(0x38)));
    }

    private static NativeBattleCureParticleStep Advance(NativeBattleCureFrame frame, uint address, NativeBattleCureParticleInputs inputs, List<NativeBattleCureOperation> operations) => NativeBattleCureParticles.AdvanceWedge8DCC20(frame, address, inputs with { Execute = (current, operation) =>
    {
        operations.Add(operation);
        return current;
    } });

    private static (NativeBattleCureFrame Frame, uint Address, NativeBattleCureParticleInputs Inputs) Fixture()
    {
        NativeBattleEffectHierarchy hierarchy = new([
            NativeBattleEffectTaskPool.Initialize(0x277AFC0, 0x277AEF8, 100, 2),
            NativeBattleEffectTaskPool.Initialize(0x278A228, 0x278C628, 88, 4),
            NativeBattleEffectTaskPool.Initialize(0x277BF70, 0x277BF80, 108, 300),
            NativeBattleEffectTaskPool.Initialize(0x278C788, 0x278A238, 92, 100)
        ]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x8D6B30, 100);
        NativeBattleEffectHierarchyAppend first = root.Hierarchy.Append8DC540(1, 0x8D6CC0, 88, root.Address, (_, _, _) => new(0, 3));
        hierarchy = first.Hierarchy;
        hierarchy = hierarchy.Write(first.Address, hierarchy.Read(first.Address).WriteWord(0x38, 100).WriteWord(0x3C, 300).WriteWord(0x4A, unchecked((ushort)-100)).WriteWord(0x52, 50));
        NativeBattleEffectHierarchyAppend wedge = hierarchy.Append8DC540(3, 0x8DCC20, 92, first.Address, (_, _, _) => new(0, 3));
        hierarchy = wedge.Hierarchy;
        NativeBattleModulePoolTask task = hierarchy.Read(wedge.Address).WriteWord(0x56, 8).WriteByte(0x4C, 16).WriteByte(0x4D, 16).WriteByte(0x4E, 16).WriteByte(0x52, 16).WriteWord(0x3C, 256).WriteWord(0x3E, 16).WriteWord(0x40, 64).WriteWord(0x42, 16).WriteWord(0x58, 64);
        hierarchy = hierarchy.Write(wedge.Address, task);
        NativeBattleOriginalEffectState effect = new(1, 0, new(1, 0x8D6A00, 0x8D69E0), new(0, 1, 8, 0, 0, 0), null) { Pools = hierarchy.Pools };
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 20000, 4096);
        NativeBattleCureFrame frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(0, 0, 0, camera));
        NativeBattleCureControllerInputs controller = new(NativeBattleActionPacketsState.Fresh(), camera, index => new(index, 0) { Transform = new(new(0, 200, 0), new(0, 200, 0), 0, 0, 4096) }, _ => throw new InvalidOperationException("Wedges use retained first-level bounds."));
        NativeBattleCureParticleInputs inputs = new(controller, NativeCrtRandomState.FromColdProcess(), (current, _) => current) { Projection = new(camera, 216, 160, 108) };
        return (frame, wedge.Address, inputs);
    }
}
