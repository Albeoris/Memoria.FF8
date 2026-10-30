using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRecoveryPresentationTests
{
    [Theory]
    [InlineData((ushort)53)]
    [InlineData((ushort)58)]
    [InlineData((ushort)59)]
    public void Recovery_mesh_overloads_preserve_all_native_groups_and_capture_exact_world_corners(ushort effect)
    {
        ImmutableArray<byte> mesh = Mesh();
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770() with { X = 300, Y = -200, Z = 1000 };
        NativeBattleActionMatrix matrix = view with { X = 320, Y = -170, Z = 5000 };
        NativeBattleCameraProjection projection = new(view, 256, 160, 108);
        NativeBattleFullLifeMeshContext context = new(0x30, null, null, null, null, null, 17, 2);
        NativeBattlePilotVisualIdentity identity = new(effect, NativeBattleRecoveryProfile.ForEffect(effect).Descriptors[2], 0, 123, 5) { ResourceGeneration = 99 };
        NativeBattleGfGeometryBatch plain = NativeBattleRecoveryMeshPackets.EmitBatch8131A0(mesh, matrix, projection, context);
        NativeBattleGfGeometryBatch captured = NativeBattleRecoveryMeshPackets.EmitBatch8131A0(mesh, matrix, projection, context, pilotIdentity: identity);
        using NativeBattleGfSourceReader reader = new(mesh);
        NativeBattleGfGeometryBatch borrowed = NativeBattleRecoveryMeshPackets.EmitBatch8131A0(reader, matrix, projection, context, pilotIdentity: identity);
        AssertNativeParity(plain, captured);
        AssertNativeParity(captured, borrowed);
        Assert.Equal(3, captured.Attempts.Length);
        for (int index = 0; index < 3; index++)
        {
            NativeBattlePilotVisualGeometry geometry = captured.Attempts[index].Primitive!.PilotGeometry!;
            Assert.Equal(effect, geometry.Identity.Effect);
            Assert.Equal(identity.Generation, geometry.Identity.Generation);
            Assert.Equal(identity.ResourceGeneration, geometry.Identity.ResourceGeneration);
            Assert.Equal(index == 0 ? 3 : 4, geometry.CornerCount);
            Assert.Equal(new Vector3(-80, -70, 4000), geometry.A);
            Assert.Equal(new Vector3(-80, 130, 4000), geometry.C);
            if (index != 0)
            {
                Assert.Equal(new Vector3(120, 130, 4000), geometry.D);
            }
            Assert.Equal(geometry, borrowed.Attempts[index].Primitive!.PilotGeometry);
        }
        Assert.Equal(3, captured.Attempts.Select(attempt => attempt.Primitive!.PilotGeometry!.Identity.Primitive).Distinct().Count());
        Assert.All(plain.Attempts, attempt => Assert.Null(attempt.Primitive!.PilotGeometry));
    }

    [Theory]
    [InlineData((ushort)7)]
    [InlineData((ushort)53)]
    [InlineData((ushort)58)]
    [InlineData((ushort)59)]
    public void Recovery_context_task_node_rebirth_and_instances_have_independent_presentation_ownership(ushort effect)
    {
        var fixture = NativeBattleRecoveryControllerTests.Fixture(effect);
        NativeBattleRecoveryProfile profile = NativeBattleRecoveryProfile.ForEffect(effect);
        NativeBattleEffectHierarchyAppend appended = new NativeBattleEffectHierarchy(fixture.Frame.Effect.Pools).Append8DC540(2, profile.GenericCallback, 676);
        int emitters = fixture.Frame.State.AuxiliaryCursor;
        int geometry = emitters + profile.EmitterBytes;
        NativeBattleRecoveryFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = appended.Hierarchy.Pools }, State = fixture.Frame.State with { Emitters = emitters, Geometry = geometry } };
        string before = NativeBattleTestSnapshots.Json(frame);
        using NativeBattleRecoveryNodeMemory memory = new(frame, appended.Address);
        NativeBattlePilotVisualIdentity task = NativeBattlePilotVisualLifetimes.Identity(effect, appended.Hierarchy.Read(appended.Address));
        memory.AllocatedPilotGeometry(geometry);
        NativeBattlePilotVisualIdentity first = memory.PilotGeometryIdentity(task, geometry, 0);
        NativeBattlePilotVisualIdentity secondInstance = memory.PilotGeometryIdentity(task, geometry, 1);
        NativeBattlePilotVisualIdentity otherRow = memory.PilotGeometryIdentity(task, geometry + 672, 0);
        memory.AllocatedPilotGeometry(geometry);
        NativeBattlePilotVisualIdentity reborn = memory.PilotGeometryIdentity(task, geometry, 0);
        Assert.Equal(effect, first.Effect);
        Assert.Equal(profile.Descriptors[2], first.Bank);
        Assert.Equal(first.Generation, reborn.Generation);
        Assert.NotEqual(first.ResourceGeneration, reborn.ResourceGeneration);
        Assert.Equal(first.ResourceGeneration, secondInstance.ResourceGeneration);
        Assert.NotEqual(first.Primitive, secondInstance.Primitive);
        Assert.Equal(otherRow, memory.PilotGeometryIdentity(task, geometry + 672, 0));
        Assert.Equal(before, NativeBattleTestSnapshots.Json(memory.Flush()));
        NativeBattleRecoveryFrame detached = NativeBattleTestSnapshots.RestoreJson<NativeBattleRecoveryFrame>(before)!;
        using NativeBattleRecoveryNodeMemory independent = new(detached, appended.Address);
        independent.AllocatedPilotGeometry(geometry);
        NativeBattlePilotVisualIdentity independentTask = NativeBattlePilotVisualLifetimes.Identity(effect, new NativeBattleEffectHierarchy(detached.Effect.Pools).Read(appended.Address));
        NativeBattlePilotVisualIdentity independentRow = independent.PilotGeometryIdentity(independentTask, geometry, 0);
        Assert.NotEqual(reborn.Generation, independentRow.Generation);
        Assert.NotEqual(reborn.ResourceGeneration, independentRow.ResourceGeneration);
        Assert.Equal(before, NativeBattleTestSnapshots.Json(independent.Flush()));
    }

    [Fact]
    public void Recovery_rejected_quads_keep_native_partial_writes_without_consuming_unreached_fourth_vertex()
    {
        byte[] bytes = Mesh().ToArray();
        W(bytes, 76 + 10, unchecked((short)0xFFFE));
        W(bytes, 116 + 10, unchecked((short)0xFFFE));
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleCameraProjection projection = new(view, 256, 160, 108);
        NativeBattleFullLifeMeshContext context = new(0x30, null, null, null, null, null, 17, 2);
        NativeBattleGfGeometryBatch plain = NativeBattleRecoveryMeshPackets.EmitBatch8131A0(bytes.ToImmutableArray(), view, projection, context);
        NativeBattleGfGeometryBatch captured = NativeBattleRecoveryMeshPackets.EmitBatch8131A0(bytes.ToImmutableArray(), view, projection, context, pilotIdentity: new(53, 123, 0, 1, 0));
        AssertNativeParity(plain, captured);
        Assert.Equal(0, captured.AllocationBytes);
        Assert.All(captured.Attempts, attempt => Assert.Null(attempt.Primitive));
    }

    private static void AssertNativeParity(NativeBattleGfGeometryBatch plain, NativeBattleGfGeometryBatch captured)
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

    private static ImmutableArray<byte> Mesh()
    {
        byte[] bytes = new byte[152];
        I32(bytes, 0, 40);
        I32(bytes, 4, 4);
        short[] coordinates = [-100, -100, 0, 0, 100, -100, 0, 0, -100, 100, 0, 0, 100, 100, 0, 0];
        for (int index = 0; index < coordinates.Length; index++)
        {
            W(bytes, 8 + index * 2, coordinates[index]);
        }
        I32(bytes, 48, 1);
        I32(bytes, 72, 1);
        I32(bytes, 112, 1);
        Face(52, 0x24808080, false);
        Face(76, 0x2C808080, true);
        Face(116, 0x3C808080, true);
        I32(bytes, 140, 0x404040);
        I32(bytes, 144, 0x606060);
        I32(bytes, 148, 0x808080);
        return bytes.ToImmutableArray();

        void Face(int offset, int command, bool quad)
        {
            I32(bytes, offset, command);
            for (int corner = 0; corner < (quad ? 4 : 3); corner++)
            {
                W(bytes, offset + 4 + corner * 2, (short)(corner * 2));
            }
        }
    }

    private static void W(byte[] bytes, int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value);
    private static void I32(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), value);
}
