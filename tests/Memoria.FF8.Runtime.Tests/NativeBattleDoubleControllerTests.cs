using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoubleControllerTests
{
    [Fact]
    public void Entry_initializes_six_native_banks_and_preserves_the_current_morph_without_reading_skipped_TIM()
    {
        var fixture = Fixture();
        Assert.Equal(0x2775058u, fixture.Frame.Effect.ReturnedDescriptor);
        Assert.Equal(new[] { 2, 4, 40, 200, 200, 4 }, fixture.Frame.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal(new ushort[] { 100, 92, 64, 104, 108, 80 }, fixture.Frame.Effect.Pools.Select(pool => pool.Stride));
        Assert.Null(fixture.Frame.State.DrawCursor);
        Assert.Null(fixture.Frame.Memory.Textures);
        Assert.Equal(fixture.Resources.Meshes.Single(mesh => mesh.Address == 0x163FFD0).Bytes.ToArray(), fixture.Frame.Memory.DoubleMorph!.Bytes.ToArray());
        Assert.Equal((byte)3, fixture.Frame.Effect.Pools[0].Slots[0].ReadByte(0x2D));
        Assert.Equal(0x1D99A78u, fixture.Frame.Effect.Pools[0].Slots[0].ReadDword(0x0C));
        NativeBattleEffectMemory nextLoad = fixture.Frame.Memory.BeginLoad(0);
        Assert.Same(fixture.Frame.Memory.DoubleMorph, nextLoad.DoubleMorph);
        Assert.Null(NativeBattleEffectMemory.FromColdProcess().DoubleMorph);
    }

    [Fact]
    public void Same_pass_spawner_publishes_first_pair_follower_and_mesh_in_physical_list_order()
    {
        var fixture = Fixture();
        List<NativeBattleDoubleOperation> operations = [];
        NativeBattleDoubleInputs inputs = fixture.Inputs with
        {
            Execute = (frame, operation) =>
            {
                operations.Add(operation);
                return frame;
            }
        };
        NativeBattleDoubleStep first = NativeBattleDoubleController.Advance8D5180(fixture.Frame, fixture.Resources, inputs, NativeCrtRandomState.FromColdProcess());
        Assert.Equal(0x6000, first.Frame.State.DrawCursor);
        Assert.Null(first.Frame.Effect.Pools[1].Head);
        NativeBattleDoubleStep second = NativeBattleDoubleController.Advance8D5180(first.Frame, fixture.Resources, inputs, first.Random);
        Assert.Equal(0, second.Frame.State.DrawCursor);
        Assert.Equal((byte)1, second.Frame.Effect.Pools[1].Slots[0].ReadByte(0x29));
        Assert.Equal((ushort)1, second.Frame.Effect.Pools[2].Slots[0].ReadWord(0x24));
        Assert.Equal(0x8D5C80u, second.Frame.Effect.Pools[4].Slots[0].Callback);
        Assert.Equal((ushort)1, second.Frame.Effect.Pools[4].Slots[0].ReadWord(0x24));
        Assert.Equal(0x8D6200u, second.Frame.Effect.Pools[3].Slots[0].Callback);
        Assert.Equal((ushort)1, second.Frame.Effect.Pools[3].Slots[0].ReadWord(0x24));
        Assert.Single(operations, operation => operation is NativeBattleDoubleSprite);
        Assert.Single(operations, operation => operation is NativeBattleDoublePackets);
        NativeBattleModuleSound sound = Assert.Single(second.Sounds);
        Assert.Equal(300027u, sound.Sound);
        Assert.Equal(0u, sound.Flags);
        Assert.Equal((byte?)128, sound.Pan);
        NativeCrtRandomState expected = first.Random.Draw(out _).Draw(out _);
        Assert.Equal(expected, second.Random);
        Assert.Equal(0x8D6490u, second.Frame.Effect.Pools[3].Slots[1].Callback);
        Assert.Equal((ushort)0, second.Frame.Effect.Pools[3].Slots[1].ReadWord(0x24));
    }

    [Fact]
    public void Complete_graph_uses_real_handshake_flash_contact_and_list_cleanup_once()
    {
        var fixture = Fixture();
        List<NativeBattleDoubleOperation> operations = [];
        List<int> contacts = [];
        List<NativeBattleModuleSound> sounds = [];
        NativeBattleDoubleInputs inputs = fixture.Inputs with
        {
            Execute = (frame, operation) =>
            {
                operations.Add(operation);
                return frame;
            },
            ApplyContact = (frame, contact) =>
            {
                Assert.Equal((ushort)20, frame.Effect.Pools[1].Slots[0].ReadWord(0x24));
                return frame with { Memory = frame.Memory with { Parameter80 = 99 } };
            }
        };
        NativeBattleDoubleFrame current = fixture.Frame;
        NativeCrtRandomState random = NativeCrtRandomState.FromColdProcess();
        int callbacks = 0;
        while (current.Effect.ReturnedDescriptor != 0 && callbacks < 200)
        {
            NativeBattleDoubleStep step = NativeBattleDoubleController.Advance8D5180(current, fixture.Resources, inputs, random);
            current = step.Frame;
            random = step.Random;
            contacts.AddRange(step.Contacts);
            sounds.AddRange(step.Sounds);
            callbacks++;
        }
        Assert.Equal(0u, current.Effect.ReturnedDescriptor);
        Assert.InRange(callbacks, 22, 199);
        Assert.Equal(new[] { 0 }, contacts);
        Assert.Single(sounds);
        Assert.Single(operations, operation => operation is NativeBattleDoubleFlash);
        Assert.Contains(operations, operation => operation is NativeBattleDoublePackets { Flags20: 1 });
        Assert.Contains(operations, operation => operation is NativeBattleDoubleSprite);
        Assert.Equal((byte?)99, current.Memory.Parameter80);
        Assert.All(current.Effect.Pools, pool => Assert.Null(pool.Head));
        NativeBattleDoubleStep idle = NativeBattleDoubleController.Advance8D5180(current, fixture.Resources, inputs, random);
        Assert.Same(current, idle.Frame);
        Assert.Equal(random, idle.Random);
        Assert.Empty(idle.Sounds);
        Assert.Empty(idle.Contacts);
    }

    [Fact]
    public void Mid_graph_JSON_preserves_morph_current_bytes_links_CRT_and_immediate_operation_order()
    {
        var fixture = Fixture();
        NativeBattleDoubleFrame current = fixture.Frame;
        NativeCrtRandomState random = NativeCrtRandomState.FromColdProcess();
        for (int callback = 0; callback < 12; callback++)
        {
            NativeBattleDoubleStep step = NativeBattleDoubleController.Advance8D5180(current, fixture.Resources, fixture.Inputs, random);
            current = step.Frame;
            random = step.Random;
        }
        string json = JsonSerializer.Serialize(current);
        NativeBattleDoubleFrame first = JsonSerializer.Deserialize<NativeBattleDoubleFrame>(json)!;
        NativeBattleDoubleFrame second = JsonSerializer.Deserialize<NativeBattleDoubleFrame>(json)!;
        List<string> left = [];
        List<string> right = [];
        NativeBattleDoubleInputs firstInputs = fixture.Inputs with
        {
            Execute = (frame, operation) =>
            {
                left.Add(JsonSerializer.Serialize(operation, operation.GetType()));
                return frame;
            }
        };
        NativeBattleDoubleInputs secondInputs = fixture.Inputs with
        {
            Execute = (frame, operation) =>
            {
                right.Add(JsonSerializer.Serialize(operation, operation.GetType()));
                return frame;
            }
        };
        NativeBattleDoubleStep a = NativeBattleDoubleController.Advance8D5180(first, fixture.Resources, firstInputs, random);
        NativeBattleDoubleStep b = NativeBattleDoubleController.Advance8D5180(second, fixture.Resources, secondInputs, random);
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
        Assert.Equal(left, right);
        Assert.Empty(a.Sounds);
    }

    [Theory]
    [InlineData(0, 0, 1536, 0)]
    [InlineData(0, 3, 0, 1)]
    [InlineData(0, 4, 768, 1)]
    [InlineData(1, 0, 4096, 2)]
    public void Primary_phase_updates_native_WORD_fade_and_completes_without_a_synthetic_duration(int phase, int repetitions, int expectedFade, int expectedPhase)
    {
        var fixture = Particle(0x8D53C0);
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(fixture.Address).WriteByte(0x29, (byte)phase).WriteWord(0x50, phase == 0 ? (ushort)2048 : (ushort)3328);
        hierarchy = hierarchy.Write(fixture.Address, task);
        NativeBattleDoubleFrame current = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        for (int callback = 0; callback <= repetitions; callback++)
        {
            NativeBattleDoubleParticleStep step = NativeBattleDoubleController.AdvancePrivate(current, fixture.Address, fixture.Resources, fixture.Inputs, NativeCrtRandomState.FromColdProcess());
            current = step.Frame;
        }
        NativeBattleModulePoolTask result = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(fixture.Address);
        Assert.Equal(unchecked((ushort)expectedFade), result.ReadWord(0x50));
        Assert.Equal((byte)expectedPhase, result.ReadByte(0x29));
    }

    [Fact]
    public void Morph_overwrites_only_XYZ_in_current_destination_and_missing_old_owner_is_strict()
    {
        byte[] first = Shape(3);
        byte[] second = Shape(5);
        byte[] destination = Mesh(1);
        destination[14] = 0xA5;
        destination[15] = 0x5A;
        NativeBattleDoubleMorph state = new(destination.ToImmutableArray());
        NativeBattleDoubleMorph next = state.Morph8D57C0(first.ToImmutableArray(), second.ToImmutableArray(), 2048);
        Assert.Equal((short)4, BinaryPrimitives.ReadInt16LittleEndian(next.Bytes.AsSpan().Slice(8)));
        Assert.Equal(new byte[] { 0xA5, 0x5A }, next.Bytes.AsSpan().Slice(14, 2).ToArray());
        Assert.Equal(destination.AsSpan(0, 8).ToArray(), next.Bytes.AsSpan().Slice(0, 8).ToArray());
        Assert.Equal(destination.AsSpan(16).ToArray(), next.Bytes.AsSpan().Slice(16).ToArray());
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleDoubleMorph>(JsonSerializer.Serialize(next))));
        var fixture = Particle(0x8D5750);
        Assert.Throws<NotSupportedException>(() => NativeBattleDoubleController.AdvancePrivate(fixture.Frame with { Memory = fixture.Frame.Memory with { DoubleMorph = null } }, fixture.Address, fixture.Resources, fixture.Inputs, NativeCrtRandomState.FromColdProcess()));
        Assert.DoesNotContain("DoubleMorph", JsonSerializer.Serialize(NativeBattleEffectMemory.FromColdProcess()));
    }

    [Fact]
    public void Shared_matrix_wrappers_preserve_full_DWORD_scales_low_WORD_results_and_translation()
    {
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770() with { X = 11, Y = 22, Z = 33 };
        NativeBattleActionMatrix scaled = NativeBattlePoseMatrices.Scale56BEF0(identity, 65537, -4096, 2048);
        Assert.Equal(new short[] { 1, 0, 0, 0, -4096, 0, 0, 0, 2048 }, scaled.Rotation);
        Assert.Equal((11, 22, 33), (scaled.X, scaled.Y, scaled.Z));
        NativeBattleActionMatrix composed = NativeBattlePoseMatrices.Compose56C2F0(identity, NativeBattleCuragaMatrices.Identity8DD770() with { X = 100, Y = -200, Z = 300 });
        Assert.Equal((111, -178, 333), (composed.X, composed.Y, composed.Z));
    }

    private static (NativeBattleDoubleFrame Frame, NativeBattleDoubleResources Resources, NativeBattleDoubleInputs Inputs, uint Address) Particle(uint callback)
    {
        var fixture = Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend target = hierarchy.Append8DC540(1, 0x8D52A0, 92, hierarchy.Address(0, 0), (_, _, _) => new(0, 3));
        NativeBattleEffectHierarchyAppend particle = target.Hierarchy.Append8DC540(4, callback, 108, target.Address, (_, _, _) => new(0, 3));
        hierarchy = particle.Hierarchy;
        NativeBattleModulePoolTask task = hierarchy.Read(particle.Address).WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096).WriteDword(0x4C, callback == 0x8D5750 ? 0x163FFD0u : 0x163F224u);
        hierarchy = hierarchy.Write(particle.Address, task);
        return (fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } }, fixture.Resources, fixture.Inputs, particle.Address);
    }

    private static (NativeBattleDoubleFrame Frame, NativeBattleDoubleResources Resources, NativeBattleDoubleInputs Inputs) Fixture()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 2, 11, 0, 33, 4, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        ImmutableArray<byte> mesh = Mesh(0).ToImmutableArray();
        NativeBattleDoubleResources resources = new(new("synthetic-unused-animation", []), [Sprite(0x163E930, 13), Sprite(0x163EA90, 8), Sprite(0x163EB6C, 1)], new uint[] { 0x163EB94, 0x163EDF4, 0x163F014, 0x163F224, 0x163FFD0 }.Select(address => new NativeBattleStaticMeshResource(0, address, mesh)).ToImmutableArray(), Shape(0, 0).ToImmutableArray(), Shape(0, 0).ToImmutableArray(), 300027);
        NativeBattleOriginalEffectState effect = new(4, 3, new(4, 0x8D5080, 0x8D5060) { DoubleResources = resources }, new(0, 1, 8, 0, 0, 0), null);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(3) with { Textures = null, DoubleMorph = new(mesh) };
        NativeBattleDoubleFrame frame = NativeBattleDoubleController.Initialize8D5080(memory, effect, packets, resources);
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), new byte[] { 1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0 }.ToImmutableArray());
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 10000 };
        NativeBattleDoubleInputs inputs = new(packets, new(view, 216, 160, 108), 0, NativeBattleGteColorInterpolation.CodeConstants45F270, index => new(index, 0) { SequenceState = new() { Pose = 0, PoseFrame114 = 1 } }, _ => source, (current, _) => current);
        return (frame, resources, inputs);
    }

    private static byte[] Shape(short x, int count = 1)
    {
        byte[] bytes = new byte[8 + count * 8];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, bytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), count);
        if (count != 0)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), x);
        }
        return bytes;
    }

    private static byte[] Mesh(int count)
    {
        byte[] shape = Shape(0, count);
        byte[] bytes = new byte[shape.Length + 32];
        shape.CopyTo(bytes, 0);
        return bytes;
    }

    private static NativeBattleModuleResource Sprite(uint address, int count)
    {
        int offset = 12 + 2 * count;
        byte[] bytes = new byte[offset + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), (ushort)count);
        for (int index = 0; index <= count; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + 2 * index), (ushort)offset);
        }
        return new(0, address, 0x570CC0, 0x570BC0, bytes.ToImmutableArray());
    }
}
