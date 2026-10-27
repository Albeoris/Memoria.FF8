using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBlizzagaFaceTests
{
    [Fact]
    public void Morph_preserves_padding_then_final_copy_and_expansion_retain_original_complete_vertices()
    {
        NativeBattleBlizzagaResources resources = Resources();
        NativeBattleBlizzagaFaceInitialization initial = Initialize(resources);
        NativeBattleModulePoolTask task = initial.Task.WriteWord(NativeBattleBlizzagaFaces.VertexOffset + 6, 1234);
        for (int phase = 28; phase <= 30; phase++)
        {
            NativeBattleBlizzagaFaceStep step = NativeBattleBlizzagaFaces.Step(initial.Memory, task, 0, phase, 0, 0, new(1), resources);
            task = step.Task;
            Assert.Equal((ushort)(4096 - (682 + (phase - 28) * 1365)), task.ReadWord(NativeBattleBlizzagaFaces.VertexOffset));
            Assert.Equal((ushort)1234, task.ReadWord(NativeBattleBlizzagaFaces.VertexOffset + 6));
        }
        NativeBattleBlizzagaFaceStep copied = NativeBattleBlizzagaFaces.Step(initial.Memory, task, 0, 31, 0, 0, new(1), resources);
        Assert.Equal(resources.ReferenceVertices.Bytes.AsSpan().Slice(8).ToArray(), NativeBattleBlizzagaFaces.ExpandedVertices(copied.Task, 31).ToArray());
        NativeBattleBlizzagaFaceStep retained = NativeBattleBlizzagaFaces.Step(copied.Memory, copied.Task, 0, 32, 0, 0, copied.Random, resources);
        Assert.Equal(NativeBattleTestSnapshots.Json(copied), NativeBattleTestSnapshots.Json(retained));
        NativeBattleBlizzagaFaceStep expanded = NativeBattleBlizzagaFaces.Step(retained.Memory, retained.Task, 0, 33, 0, 0, retained.Random, resources);
        ImmutableArray<byte> mesh = NativeBattleBlizzagaFaces.ReadPrimaryMesh(expanded.Memory, 0);
        ImmutableArray<byte> companion = NativeBattleBlizzagaFaces.Companion(expanded.Memory);
        ImmutableArray<byte> vertices = NativeBattleBlizzagaFaces.ExpandedVertices(expanded.Task, 33);
        for (int face = 0; face < 320; face++)
        {
            for (int corner = 0; corner < 3; corner++)
            {
                ushort selector = BinaryPrimitives.ReadUInt16LittleEndian(mesh.AsSpan().Slice(0x24 + face * 28 + 4 + corner * 2));
                Assert.Equal((face * 3 + corner) * 2, selector);
                Assert.Equal(selector, BinaryPrimitives.ReadUInt16LittleEndian(companion.AsSpan().Slice(0x14 + face * 20 + 4 + corner * 2)));
                Assert.Equal(resources.ReferenceVertices.Bytes.AsSpan().Slice(8 + corner * 8, 8).ToArray(), vertices.AsSpan().Slice((face * 3 + corner) * 8, 8).ToArray());
            }
            Assert.Equal(1, expanded.Task.ReadWord(0x28 + face * 0x38 + 14));
        }
    }

    [Fact]
    public void Activation_fades_all_three_colors_together_and_does_not_reactivate_retired_faces()
    {
        NativeBattleBlizzagaResources resources = Resources();
        NativeBattleBlizzagaFaceInitialization initial = Initialize(resources);
        NativeBattleBlizzagaFaceStep step = NativeBattleBlizzagaFaces.Step(initial.Memory, initial.Task, 0, 33, -1000, 0, new(1), resources);
        step = NativeBattleBlizzagaFaces.Step(step.Memory, step.Task, 0, 51, -1000, 0, step.Random, resources);
        NativeCrtRandomState expected = new(1);
        for (int draw = 0; draw < 1280; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, step.Random);
        ImmutableArray<byte> mesh = NativeBattleBlizzagaFaces.ReadPrimaryMesh(step.Memory, 0);
        int decay = unchecked((short)step.Task.ReadWord(0x28 + 0x36));
        Assert.Equal(128 - decay, mesh[0x24]);
        Assert.Equal(128 - decay, mesh[0x24 + 0x14]);
        Assert.Equal(128 - decay, mesh[0x24 + 0x18]);
        Assert.Equal(128, mesh[0x24 + 0x17]);
        Assert.Equal(0, mesh[0x24 + 0x1B]);
        Assert.Equal(0x1234, BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleBlizzagaFaces.ExpandedVertices(step.Task, 51).AsSpan().Slice(6)));
        NativeBattleBlizzagaFaceStep restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleBlizzagaFaceStep>(NativeBattleTestSnapshots.Json(step))!;
        for (int phase = 52; phase <= 97; phase++)
        {
            step = NativeBattleBlizzagaFaces.Step(step.Memory, step.Task, 0, phase, -1000, 0, step.Random, resources);
            restored = NativeBattleBlizzagaFaces.Step(restored.Memory, restored.Task, 0, phase, -1000, 0, restored.Random, resources);
            Assert.Equal(NativeBattleTestSnapshots.Json(step), NativeBattleTestSnapshots.Json(restored));
        }
        Assert.Equal(expected, step.Random);
        for (int face = 0; face < 320; face++)
        {
            Assert.Equal(1, step.Task.ReadWord(0x28 + face * 0x38 + 6));
            Assert.Equal(0, step.Task.ReadWord(0x28 + face * 0x38 + 14));
        }
    }

    [Fact]
    public void Frame82_forces_pending_faces_and_extent_keeps_first_Z_and_native_else_if()
    {
        NativeBattleBlizzagaResources resources = Resources();
        Assert.Equal(((short)-1000, (short)1062), NativeBattleBlizzagaFaces.Extents(resources));
        NativeBattleBlizzagaFaceInitialization initial = Initialize(resources);
        NativeBattleBlizzagaFaceStep step = NativeBattleBlizzagaFaces.Step(initial.Memory, initial.Task, 0, 33, -32000, 0, new(1), resources);
        step = NativeBattleBlizzagaFaces.Step(step.Memory, step.Task, 0, 81, -32000, 0, step.Random, resources);
        Assert.Equal(new NativeCrtRandomState(1), step.Random);
        step = NativeBattleBlizzagaFaces.Step(step.Memory, step.Task, 0, 82, -32000, 0, step.Random, resources);
        Assert.NotEqual(new NativeCrtRandomState(1), step.Random);
        Assert.All(Enumerable.Range(0, 320), face => Assert.Equal(1, step.Task.ReadWord(0x28 + face * 0x38 + 6)));
    }

    internal static NativeBattleBlizzagaResources Resources()
    {
        byte[] mesh = new byte[0x2328];
        byte[] companion = new byte[0x1928];
        foreach (byte[] geometry in new[] { mesh, companion })
        {
            BinaryPrimitives.WriteUInt32LittleEndian(geometry, 8);
            BinaryPrimitives.WriteUInt32LittleEndian(geometry.AsSpan(4), 177);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(0x20), 320);
        BinaryPrimitives.WriteUInt32LittleEndian(companion.AsSpan(0x10), 320);
        for (int face = 0; face < 320; face++)
        {
            int offset = 0x24 + face * 28;
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(offset), 0x34808080);
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(offset + 6), 2);
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(offset + 8), 4);
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(offset + 0x14), 0x00808080);
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(offset + 0x18), 0x00808080);
            BinaryPrimitives.WriteUInt32LittleEndian(companion.AsSpan(0x14 + face * 20), 0x24808080);
        }
        byte[] reference = new byte[0x590];
        BinaryPrimitives.WriteUInt32LittleEndian(reference, 0x590);
        BinaryPrimitives.WriteUInt32LittleEndian(reference.AsSpan(4), 177);
        for (int vertex = 0; vertex < 177; vertex++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(reference.AsSpan(10 + vertex * 8), -1000);
            BinaryPrimitives.WriteUInt16LittleEndian(reference.AsSpan(14 + vertex * 8), 0x1234);
        }
        BinaryPrimitives.WriteInt16LittleEndian(reference.AsSpan(16), 64);
        BinaryPrimitives.WriteInt16LittleEndian(reference.AsSpan(28), 64);
        byte[] source = (byte[])reference.Clone();
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(8), 4096);
        NativeBattleBlizzaraResources basic = NativeBattleBlizzaraFaceTests.Resources();
        return new(basic.Animation, basic.Sprite16, basic.Sprite16, new(1, 177, mesh.ToImmutableArray()), new(2, 177, companion.ToImmutableArray()), new(3, source.ToImmutableArray()), new(4, reference.ToImmutableArray()), basic.SkyMesh, basic.RingMesh, 300089);
    }

    private static NativeBattleBlizzagaFaceInitialization Initialize(NativeBattleBlizzagaResources resources) => NativeBattleBlizzagaFaces.Initialize(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, new(1, 0, null, 0x6D67E0, new byte[0x6428 - 12].ToImmutableArray()), 0, resources);
}
