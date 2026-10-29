using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGroup9EnemyPresentationTests
{
    [Fact]
    public void Storm_stream_captures_actual_flat_and_gouraud_quad_selectors_without_changing_windows_or_writes()
    {
        byte[] source = new byte[2600];
        I32(source, 0, 2500);
        I32(source, 2512, 1);
        I32(source, 2552, 1);
        Face(2516, 0x2C808080);
        Face(2556, 0x3C808080);
        I32(source, 2580, 0x404040);
        I32(source, 2584, 0x606060);
        I32(source, 2588, 0x808080);
        byte[] vertices = new byte[308 * 8];
        Vector(0, -100, -100);
        Vector(1, 100, -100);
        Vector(2, -100, 100);
        Vector(3, 100, 100);
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleCameraProjection projection = new(view, 256, 160, 108);
        NativeBattleSummonColors colors = NativeBattleGteColorInterpolation.CodeConstants45F270;
        NativeBattlePilotVisualIdentity identity = new(11, 0x2556198, 0, 71, 0x10000);
        NativeBattleGfGeometryBatch plain = NativeBattleStorm11Geometry.Emit6EB6A0(source.ToImmutableArray(), vertices.ToImmutableArray(), view, projection, 10, colors);
        NativeBattleGfGeometryBatch captured = NativeBattleStorm11Geometry.Emit6EB6A0(source.ToImmutableArray(), vertices.ToImmutableArray(), view, projection, 10, colors, identity);
        AssertParity(plain, captured);
        NativeBattlePilotVisualGeometry[] geometry = captured.Attempts.Where(attempt => attempt.Primitive?.PilotGeometry is not null).Select(attempt => attempt.Primitive!.PilotGeometry!).ToArray();
        Assert.Equal(2, geometry.Length);
        Assert.NotEqual(geometry[0].Identity.Primitive, geometry[1].Identity.Primitive);
        Assert.All(geometry, quad =>
        {
            Assert.Equal(new Vector3(-100, -100, 4096), quad.A);
            Assert.Equal(new Vector3(100, 100, 4096), quad.D);
            Assert.Equal(4, quad.CornerCount);
            Assert.Equal(71, quad.Identity.Generation);
        });
        Assert.Equal(4, captured.Attempts.Count(attempt => attempt.Primitive is { } primitive && primitive.PilotGeometry is null));
        NativeBattleGfGeometryBatch control = NativeBattleStorm11Geometry.Emit6CF0C0(source.ToImmutableArray(), vertices.ToImmutableArray(), view, projection, 10, colors);
        AssertParity(plain, control);
        Assert.All(control.Attempts, attempt => Assert.Null(attempt.Primitive?.PilotGeometry));

        void Face(int offset, uint command)
        {
            I32(source, offset, unchecked((int)command));
            for (int corner = 0; corner < 4; corner++)
            {
                W(source, offset + 4 + corner * 2, (short)(corner * 2));
            }
        }
        void Vector(int index, short x, short y)
        {
            W(vertices, index * 8, x);
            W(vertices, index * 8 + 2, y);
            W(vertices, index * 8 + 4, 4096);
        }
    }

    [Fact]
    public void RayBomb_ring_operation_keeps_its_actual_task_generation_while_native_counter_advances()
    {
        NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(0x24C91F8, 0x24C91D8, 16, 2);
        (roots, _) = roots.Append(0x629730);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x24C9988, 0x24C9998, 36, 100);
        (children, int slot) = children.Append(0x628870);
        children = children.WriteSlot(slot, children.Slots[slot].WriteWord(28, 4096).WriteWord(32, 4096));
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(9) with { RayBomb10 = NativeBattleRayBomb10State.FromColdProcess() with { ArenaBase = 0x20E1AB8 } };
        NativeBattleOriginalEffectState effect = new(10, 9, new(10, 0x627E60, 0x627E40), new(3, 0, 1, 0, 0, 0), null) { ReturnedDescriptor = 0x24C91F8, Pools = [roots, children] };
        NativeBattleRayBomb10Frame frame = new(memory, effect, new(123));
        NativeBattleRayBomb10Resources resources = new(new("camera", []), [], [], [], [], [], [], [], [], 0, 0);
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770();
        List<NativeBattleRayBomb10Mesh> meshes = [];
        NativeBattleRayBomb10Inputs inputs = new(0, NativeBattleActionPacketsState.Fresh(), new(view, 256, 160, 108), 0, new(0, 0, 0), index => new(index, 0), _ => view, (_, _, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (current, operation) =>
        {
            meshes.Add(Assert.IsType<NativeBattleRayBomb10Mesh>(operation));
            return current;
        });
        frame = NativeBattleRayBomb10Controller.Advance629730(frame, resources, inputs);
        NativeBattlePilotVisualIdentity first = meshes[0].PilotIdentity!.Value;
        frame = NativeBattleRayBomb10Controller.Advance629730(frame, resources, inputs);
        Assert.Equal(first, meshes[1].PilotIdentity!.Value);
        Assert.Equal(0x24C9988u, first.Bank);
        Assert.Equal(slot, first.Slot);
        Assert.Equal((ushort)2, frame.Effect.Pools[1].Slots[slot].ReadWord(12));
        Assert.Equal(new NativeCrtRandomState(123), frame.Random);
    }

    [Fact]
    public void RayBomb_independent_particle_rebirth_does_not_reuse_previous_row_identity()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x24C91F8, 0x24C91D8, 16, 2);
        (pool, int slot) = pool.Append(0x629730);
        NativeBattleModulePoolTask root = pool.Slots[slot];
        NativeBattleRayBomb10Presentation.Allocated(root, 7);
        NativeBattlePilotVisualIdentity before = NativeBattleRayBomb10Presentation.Identity(root, 7);
        NativeBattlePilotVisualIdentity neighbor = NativeBattleRayBomb10Presentation.Identity(root, 8);
        NativeBattleRayBomb10Presentation.Allocated(root, 7);
        NativeBattlePilotVisualIdentity after = NativeBattleRayBomb10Presentation.Identity(root, 7);
        Assert.Equal(before.Generation, after.Generation);
        Assert.NotEqual(before.ResourceGeneration, after.ResourceGeneration);
        Assert.Equal(neighbor, NativeBattleRayBomb10Presentation.Identity(root, 8));
        Assert.Equal((ushort)0, root.ReadWord(12));
    }

    private static void AssertParity(NativeBattleGfGeometryBatch plain, NativeBattleGfGeometryBatch captured)
    {
        Assert.Equal(plain.AllocationBytes, captured.AllocationBytes);
        Assert.Equal(plain.Attempts.Length, captured.Attempts.Length);
        for (int index = 0; index < plain.Attempts.Length; index++)
        {
            Assert.Equal(plain.Attempts[index].Data.ToArray(), captured.Attempts[index].Data.ToArray());
            Assert.Equal(plain.Attempts[index].WriteMask.ToArray(), captured.Attempts[index].WriteMask.ToArray());
            Assert.Equal(plain.Attempts[index].ByteOffsetRelative, captured.Attempts[index].ByteOffsetRelative);
            Assert.Equal(plain.Attempts[index].Primitive?.Bucket, captured.Attempts[index].Primitive?.Bucket);
        }
    }

    private static void I32(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), value);
    private static void W(byte[] bytes, int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value);
}
