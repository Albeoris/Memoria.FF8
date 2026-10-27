using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBlizzaraFaceTests
{
    [Fact]
    public void Expansion_retains_complete_vertices_and_isolates_three_mutable_mesh_pairs()
    {
        NativeBattleBlizzaraResources resources = Resources();
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() };
        for (int sequence = 0; sequence < 3; sequence++)
        {
            NativeBattleBlizzaraFaceInitialization initialized = NativeBattleBlizzaraFaces.Initialize(memory, Task(), sequence, resources);
            memory = initialized.Memory;
            byte[] mesh = NativeBattleBlizzaraFaces.ReadPrimaryMesh(memory, sequence).ToArray();
            ImmutableArray<byte> vertices = NativeBattleBlizzaraFaces.ExpandedVertices(initialized.Task);
            for (int face = 0; face < 223; face++)
            {
                for (int corner = 0; corner < 3; corner++)
                {
                    Assert.Equal((face * 3 + corner) * 2, BinaryPrimitives.ReadUInt16LittleEndian(mesh.AsSpan(0x454 + face * 28 + 4 + corner * 2)));
                    Assert.Equal(resources.PrimaryMesh.Bytes.AsSpan().Slice(8 + corner * 8, 8).ToArray(), vertices.AsSpan().Slice((face * 3 + corner) * 8, 8).ToArray());
                }
            }
        }
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(resources.PrimaryMesh.Bytes.AsSpan().Slice(0x458)));
        Assert.Equal(2, BinaryPrimitives.ReadUInt16LittleEndian(resources.PrimaryMesh.Bytes.AsSpan().Slice(0x45A)));
        Assert.Equal(4, BinaryPrimitives.ReadUInt16LittleEndian(resources.PrimaryMesh.Bytes.AsSpan().Slice(0x45C)));
    }

    [Fact]
    public void Activation_consumes_three_random_draws_per_face_and_fades_colors_sequentially()
    {
        NativeBattleBlizzaraFaceInitialization initialized = NativeBattleBlizzaraFaces.Initialize(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, Task(), 0, Resources());
        NativeBattleBlizzaraFaceStep step = new(initialized.Memory, initialized.Task, new(1));
        for (int phase = 30; phase <= 44; phase++)
        {
            step = NativeBattleBlizzaraFaces.Advance(step.Memory, step.Task, 0, phase, step.Random);
        }
        Assert.Equal(new NativeCrtRandomState(1), step.Random);
        step = NativeBattleBlizzaraFaces.Advance(step.Memory, step.Task, 0, 45, step.Random);
        NativeCrtRandomState expected = new(1);
        for (int draw = 0; draw < 669; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, step.Random);
        ImmutableArray<byte> mesh = NativeBattleBlizzaraFaces.ReadPrimaryMesh(step.Memory, 0);
        Assert.Equal(111, mesh[0x454]);
        Assert.Equal(128, mesh[0x454 + 0x14]);
        Assert.Equal(223, mesh[0x454 + 0x17]);
        Assert.Equal(101, BinaryPrimitives.ReadInt16LittleEndian(NativeBattleBlizzaraFaces.ExpandedVertices(step.Task).AsSpan().Slice(6)));
        for (int phase = 46; phase <= 70; phase++)
        {
            step = NativeBattleBlizzaraFaces.Advance(step.Memory, step.Task, 0, phase, step.Random);
        }
        Assert.Equal(expected, step.Random);
        for (int face = 0; face < 223; face++)
        {
            Assert.Equal(0, step.Task.ReadWord(0x32 + face * 0x48));
        }
    }

    [Fact]
    public void Active_rows_clones_expanded_vertices_and_RNG_restore_before_the_next_activation_batch()
    {
        NativeBattleBlizzaraResources resources = Resources();
        byte[] primary = resources.PrimaryMesh.Bytes.ToArray();
        for (int corner = 0; corner < 3; corner++)
        {
            primary.AsSpan(8 + corner * 8, 8).CopyTo(primary.AsSpan(8 + (3 + corner) * 8));
            BinaryPrimitives.WriteInt16LittleEndian(primary.AsSpan(10 + (3 + corner) * 8), -500);
            BinaryPrimitives.WriteInt16LittleEndian(primary.AsSpan(14 + (3 + corner) * 8), (short)(201 + corner));
        }
        for (int face = 111; face < 223; face++)
        {
            for (int corner = 0; corner < 3; corner++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(primary.AsSpan(0x454 + face * 28 + 4 + corner * 2), checked((ushort)((3 + corner) * 2)));
            }
        }
        resources = resources with { PrimaryMesh = resources.PrimaryMesh with { Bytes = primary.ToImmutableArray() } };
        NativeBattleBlizzaraFaceInitialization initialized = NativeBattleBlizzaraFaces.Initialize(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, Task(), 1, resources);
        NativeBattleBlizzaraFaceStep step = new(initialized.Memory, initialized.Task, new(1));
        for (int phase = 30; phase <= 47; phase++)
        {
            step = NativeBattleBlizzaraFaces.Advance(step.Memory, step.Task, 1, phase, step.Random);
        }
        Assert.Equal((ushort)1, step.Task.ReadWord(0x32));
        Assert.Equal((ushort)0, step.Task.ReadWord(0x32 + 111 * 0x48));
        string json = NativeBattleTestSnapshots.Json(step);
        NativeBattleBlizzaraFaceStep restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleBlizzaraFaceStep>(json)!;
        Assert.Equal(json, NativeBattleTestSnapshots.Json(restored));
        NativeBattleBlizzaraFaceStep predecessor = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(step));
        NativeBattleBlizzaraFaceStep first = NativeBattleBlizzaraFaces.Advance(predecessor.Memory, predecessor.Task, 1, 48, predecessor.Random);
        NativeBattleBlizzaraFaceStep second = NativeBattleBlizzaraFaces.Advance(restored.Memory, restored.Task, 1, 48, restored.Random);
        Assert.Equal(NativeBattleTestSnapshots.Json(first), NativeBattleTestSnapshots.Json(second));
        Assert.Equal((ushort)1, second.Task.ReadWord(0x32 + 111 * 0x48));
        Assert.NotEqual(step.Random, second.Random);
        NativeCrtRandomState expected = step.Random;
        for (int draw = 0; draw < 112 * 3; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, second.Random);
        ImmutableArray<byte> vertices = NativeBattleBlizzaraFaces.ExpandedVertices(second.Task);
        Assert.Equal(101, BinaryPrimitives.ReadInt16LittleEndian(vertices.AsSpan().Slice(6)));
        Assert.Equal(201, BinaryPrimitives.ReadInt16LittleEndian(vertices.AsSpan().Slice(111 * 3 * 8 + 6)));
        Assert.Equal(json, NativeBattleTestSnapshots.Json(step));
    }

    [Fact]
    public void RGB_fading_waits_for_all_channels_before_advancing_the_next_vertex_and_stops_physics_when_done()
    {
        NativeBattleBlizzaraResources resources = Resources();
        byte[] primary = resources.PrimaryMesh.Bytes.ToArray();
        new byte[] { 16, 32, 48 }.CopyTo(primary, 0x454);
        new byte[] { 8, 16, 24 }.CopyTo(primary, 0x454 + 0x14);
        new byte[] { 24, 16, 8 }.CopyTo(primary, 0x454 + 0x18);
        resources = resources with { PrimaryMesh = resources.PrimaryMesh with { Bytes = primary.ToImmutableArray() } };
        NativeBattleBlizzaraFaceInitialization initialized = NativeBattleBlizzaraFaces.Initialize(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, Task(), 0, resources);
        NativeBattleBlizzaraFaceStep step = new(initialized.Memory, initialized.Task, new(1));
        for (int phase = 30; phase <= 61; phase++)
        {
            NativeBattleBlizzaraFaceStep prior = step;
            step = NativeBattleBlizzaraFaces.Advance(step.Memory, step.Task, 0, phase, step.Random);
            switch (phase)
            {
                case 50:
                    AssertColors(step, [0, 2, 6], [8, 16, 24], [24, 16, 8]);
                    break;
                case 51:
                    AssertColors(step, [0, 0, 0], [6, 13, 20], [24, 16, 8]);
                    break;
                case 55:
                    AssertColors(step, [0, 0, 0], [0, 1, 4], [24, 16, 8]);
                    break;
                case 56:
                    AssertColors(step, [0, 0, 0], [0, 0, 0], [20, 13, 6]);
                    break;
                case 60:
                    AssertColors(step, [0, 0, 0], [0, 0, 0], [4, 1, 0]);
                    Assert.Equal((ushort)1, step.Task.ReadWord(0x32));
                    break;
                case 61:
                    AssertColors(step, [0, 0, 0], [0, 0, 0], [0, 0, 0]);
                    Assert.Equal((ushort)0, step.Task.ReadWord(0x32));
                    Assert.Equal(prior.Task.ReadWord(0x36), step.Task.ReadWord(0x36));
                    Assert.Equal(NativeBattleBlizzaraFaces.ExpandedVertices(prior.Task).AsSpan().Slice(0, 24).ToArray(), NativeBattleBlizzaraFaces.ExpandedVertices(step.Task).AsSpan().Slice(0, 24).ToArray());
                    break;
            }
        }
    }

    [Fact]
    public void Synthetic_face_banks_have_the_exact_original_directory_extents_and_complete_source_vertices()
    {
        NativeBattleBlizzaraResources resources = Resources();
        resources.Sprite16.Validate();
        Assert.Equal(16, BinaryPrimitives.ReadUInt16LittleEndian(resources.Sprite16.Bytes.AsSpan().Slice(8)));
        Assert.Equal(0x1CBC, resources.PrimaryMesh.Bytes.Length);
        Assert.Equal(0x1194, resources.PartnerMesh.Bytes.Length);
        Assert.Equal(134, BinaryPrimitives.ReadInt32LittleEndian(resources.PrimaryMesh.Bytes.AsSpan().Slice(4)));
        for (int group = 0; group < 8; group++)
        {
            Assert.Equal(group == 6 ? 223 : 0, NativeBattleIceMeshVertices.Group(resources.PrimaryMesh.Bytes.AsSpan(), group).Count);
            Assert.Equal(group == 2 ? 223 : 0, NativeBattleIceMeshVertices.Group(resources.PartnerMesh.Bytes.AsSpan(), group).Count);
        }
        Assert.Equal(0x24, resources.PartnerMesh.Bytes[0x14 + 3]);
    }

    private static void AssertColors(NativeBattleBlizzaraFaceStep step, byte[] first, byte[] second, byte[] third)
    {
        ImmutableArray<byte> mesh = NativeBattleBlizzaraFaces.ReadPrimaryMesh(step.Memory, 0);
        Assert.Equal(first, mesh.AsSpan().Slice(0x454, 3).ToArray());
        Assert.Equal(second, mesh.AsSpan().Slice(0x454 + 0x14, 3).ToArray());
        Assert.Equal(third, mesh.AsSpan().Slice(0x454 + 0x18, 3).ToArray());
    }

    internal static NativeBattleBlizzaraResources Resources()
    {
        byte[] sprite = new byte[68];
        BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8), 16);
        for (int frame = 0; frame <= 16; frame++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + frame * 2), 44);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(sprite.AsSpan(44), 1);
        sprite[48] = 1;
        sprite[49] = 128;
        sprite[50] = 1;
        sprite[51] = 0x2C;
        byte[] primary = new byte[0x1CBC];
        BinaryPrimitives.WriteUInt32LittleEndian(primary, 0x438);
        BinaryPrimitives.WriteUInt32LittleEndian(primary.AsSpan(4), 134);
        for (int corner = 0; corner < 3; corner++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(primary.AsSpan(8 + corner * 8), (short)(corner == 1 ? 64 : 0));
            BinaryPrimitives.WriteInt16LittleEndian(primary.AsSpan(10 + corner * 8), -1000);
            BinaryPrimitives.WriteInt16LittleEndian(primary.AsSpan(12 + corner * 8), (short)(corner == 2 ? 64 : 0));
            BinaryPrimitives.WriteInt16LittleEndian(primary.AsSpan(14 + corner * 8), (short)(101 + corner));
        }
        BinaryPrimitives.WriteUInt32LittleEndian(primary.AsSpan(0x450), 223);
        byte[] partner = new byte[0x1194];
        BinaryPrimitives.WriteUInt32LittleEndian(partner, 8);
        BinaryPrimitives.WriteUInt32LittleEndian(partner.AsSpan(0x10), 223);
        for (int face = 0; face < 223; face++)
        {
            int offset = 0x454 + face * 28;
            BinaryPrimitives.WriteUInt32LittleEndian(primary.AsSpan(offset), 0x34808080);
            BinaryPrimitives.WriteUInt16LittleEndian(primary.AsSpan(offset + 6), 2);
            BinaryPrimitives.WriteUInt16LittleEndian(primary.AsSpan(offset + 8), 4);
            BinaryPrimitives.WriteUInt32LittleEndian(primary.AsSpan(offset + 0x14), 0xAB808080);
            BinaryPrimitives.WriteUInt32LittleEndian(primary.AsSpan(offset + 0x18), 0xCD808080);
            BinaryPrimitives.WriteUInt32LittleEndian(partner.AsSpan(0x14 + face * 20), 0x24808080);
        }
        return new(new("animation", []), new(1, 1, 0, 0, sprite.ToImmutableArray()), new(1, 1, []), new(2, 2, []), new(3, 3, primary.ToImmutableArray()), new(4, 4, partner.ToImmutableArray()), 300015);
    }

    private static NativeBattleModulePoolTask Task() => new(1, 0, null, 0x6DAA30, new byte[0x53CC - 12].ToImmutableArray());
}
