using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLifeControllerTests
{
    [Theory]
    [InlineData(35, 0x8649C0u, 0x26AC868u)]
    [InlineData(5, 0x8D4450u, 0x2761988u)]
    public void Original_entry_initializes_all_five_physical_banks_and_first_contact_actor(ushort effect, uint entry, uint descriptor)
    {
        var fixture = Fixture(effect);
        Assert.Equal(entry, fixture.Frame.Effect.Binding.Entry);
        Assert.Equal(descriptor, fixture.Frame.Effect.ReturnedDescriptor);
        Assert.Equal(new[] { 2, 4, 300, 300, 100 }, fixture.Frame.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal(new ushort[] { 100, 100, 48, 112, 92 }, fixture.Frame.Effect.Pools.Select(pool => pool.Stride));
        NativeBattleModulePoolTask root = fixture.Frame.Effect.Pools[0].Slots[0];
        Assert.Equal(0x1D99A78u, root.ReadDword(0x0C));
        Assert.Equal((byte)3, root.ReadByte(0x2D));
        Assert.Equal((ushort)1, root.ReadWord(0x5A));
        Assert.Equal((ushort)0, root.ReadWord(0x58));
        Assert.Null(fixture.Frame.State.DrawCursor);
        Assert.Null(fixture.Frame.Memory.Textures!.Animation);
        Assert.Equal(effect == 5 ? 1 : 0, fixture.Frame.Memory.Textures.Count);
    }

    [Theory]
    [InlineData(35)]
    [InlineData(5)]
    public void Same_pass_target_spawn_and_sprite_callbacks_use_five_CRT_draws_and_one_sound(ushort effect)
    {
        var fixture = Fixture(effect);
        NativeBattleLifeStep first = NativeBattleLifeController.Advance864AB0(fixture.Frame, fixture.Resources, fixture.Inputs);
        Assert.Null(first.Frame.Effect.Pools[1].Head);
        Assert.Equal(0x2000, first.Frame.State.DrawCursor);
        NativeBattleLifeStep second = NativeBattleLifeController.Advance864AB0(first.Frame, fixture.Resources, fixture.Inputs with { Random = first.Random });
        Assert.Equal(0, second.Frame.State.DrawCursor);
        Assert.Equal((byte)1, second.Frame.Effect.Pools[1].Slots[0].ReadByte(0x29));
        Assert.Equal((ushort)1, second.Frame.Effect.Pools[1].Slots[0].ReadWord(0x24));
        Assert.Equal(2, second.Frame.Effect.Pools[3].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.All(second.Frame.Effect.Pools[3].Slots.Where(task => (task.Flags & 1) != 0), task =>
        {
            Assert.Equal((byte)1, task.ReadByte(0x29));
            Assert.Equal((ushort)1, task.ReadWord(0x24));
        });
        NativeCrtRandomState expected = fixture.Inputs.Random;
        for (int draw = 0; draw < 5; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, second.Random);
        NativeBattleModuleSound sound = Assert.Single(second.Sounds);
        Assert.Equal(300021u, sound.Sound);
        Assert.Equal(0u, sound.Flags);
        Assert.Equal((byte?)128, sound.Pan);
        Assert.Empty(second.Contacts);
    }

    [Theory]
    [InlineData(35)]
    [InlineData(5)]
    public void Full_original_lists_finish_after_one_strictly_greater_than_thirty_contact_and_one_sound(ushort effect)
    {
        var fixture = Fixture(effect);
        NativeBattleLifeFrame frame = fixture.Frame;
        NativeCrtRandomState random = fixture.Inputs.Random;
        List<int> contacts = [];
        List<NativeBattleModuleSound> sounds = [];
        int applied = 0;
        NativeBattleLifeInputs inputs = fixture.Inputs with
        {
            ApplyContact = (current, contact) =>
            {
                Assert.Equal(0, contact);
                NativeBattleModulePoolTask target = current.Effect.Pools[1].Slots[0];
                Assert.Equal((ushort)31, target.ReadWord(0x24));
                applied++;
                return current with { Memory = current.Memory with { Parameter80 = 77 } };
            }
        };
        int callbacks = 0;
        while (frame.Effect.ReturnedDescriptor != 0 && callbacks < 120)
        {
            NativeBattleLifeStep step = NativeBattleLifeController.Advance864AB0(frame, fixture.Resources, inputs with { Random = random });
            contacts.AddRange(step.Contacts);
            sounds.AddRange(step.Sounds);
            frame = step.Frame;
            random = step.Random;
            callbacks++;
        }
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.InRange(callbacks, 34, 119);
        Assert.Equal(1, applied);
        Assert.Single(contacts);
        Assert.Single(sounds);
        Assert.Equal((byte?)77, frame.Memory.Parameter80);
        Assert.All(frame.Effect.Pools, pool => Assert.Null(pool.Head));
        Assert.Equal((byte)0, frame.Effect.Pools[0].Slots[0].ReadByte(0x28));
        NativeBattleLifeStep idle = NativeBattleLifeController.Advance864AB0(frame, fixture.Resources, inputs with { Random = random });
        Assert.Same(frame, idle.Frame);
        Assert.Empty(idle.Sounds);
        Assert.Empty(idle.Contacts);
        Assert.Equal(random, idle.Random);
    }

    [Fact]
    public void Both_normalized_profiles_keep_the_same_callback_phase_physics_and_CRT_trajectory()
    {
        var life = Fixture(35);
        var phoenix = Fixture(5);
        NativeBattleLifeFrame left = life.Frame;
        NativeBattleLifeFrame right = phoenix.Frame;
        NativeCrtRandomState leftRandom = life.Inputs.Random;
        NativeCrtRandomState rightRandom = phoenix.Inputs.Random;
        for (int callback = 0; callback < 45; callback++)
        {
            NativeBattleLifeStep a = NativeBattleLifeController.Advance864AB0(left, life.Resources, life.Inputs with { Random = leftRandom });
            NativeBattleLifeStep b = NativeBattleLifeController.Advance864AB0(right, phoenix.Resources, phoenix.Inputs with { Random = rightRandom });
            left = a.Frame;
            right = b.Frame;
            leftRandom = a.Random;
            rightRandom = b.Random;
            Assert.Equal(leftRandom, rightRandom);
            Assert.Equal(a.Contacts.ToArray(), b.Contacts.ToArray());
            Assert.Equal(a.Sounds.ToArray(), b.Sounds.ToArray());
            Assert.Equal(left.State, right.State);
            for (int pool = 0; pool < 5; pool++)
            {
                Assert.Equal(left.Effect.Pools[pool].Head, right.Effect.Pools[pool].Head);
                for (int slot = 0; slot < left.Effect.Pools[pool].Slots.Length; slot++)
                {
                    NativeBattleModulePoolTask first = left.Effect.Pools[pool].Slots[slot];
                    NativeBattleModulePoolTask second = right.Effect.Pools[pool].Slots[slot];
                    Assert.Equal(first.Flags, second.Flags);
                    Assert.Equal(first.Next, second.Next);
                    Assert.Equal(first.Payload.AsSpan().Slice(0x1C - 12, 0x14).ToArray(), second.Payload.AsSpan().Slice(0x1C - 12, 0x14).ToArray());
                }
            }
        }
    }

    [Fact]
    public void Mid_cast_JSON_uses_saved_current_banks_and_CRT_without_reinitialization_or_sound_replay()
    {
        var fixture = Fixture();
        NativeBattleLifeFrame frame = fixture.Frame;
        NativeCrtRandomState random = fixture.Inputs.Random;
        for (int callback = 0; callback < 12; callback++)
        {
            NativeBattleLifeStep next = NativeBattleLifeController.Advance864AB0(frame, fixture.Resources, fixture.Inputs with { Random = random });
            frame = next.Frame;
            random = next.Random;
        }
        string json = JsonSerializer.Serialize(frame);
        NativeBattleLifeFrame first = JsonSerializer.Deserialize<NativeBattleLifeFrame>(json)!;
        NativeBattleLifeFrame second = JsonSerializer.Deserialize<NativeBattleLifeFrame>(json)!;
        NativeBattleLifeStep a = NativeBattleLifeController.Advance864AB0(first, fixture.Resources, fixture.Inputs with { Random = random });
        NativeBattleLifeStep b = NativeBattleLifeController.Advance864AB0(second, fixture.Resources, fixture.Inputs with { Random = random });
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
        Assert.Empty(a.Sounds);
        Assert.NotEqual(json, JsonSerializer.Serialize(a.Frame));
    }

    [Theory]
    [InlineData(0, 0, 0, true)]
    [InlineData(14, 15, 15, false)]
    [InlineData(15, 15, 15, true)]
    [InlineData(32767, 15, -32768, false)]
    [InlineData(-2, -1, -1, false)]
    public void Sprite_frame_increment_compares_signed_WORDs_after_wrapping(int old, int duration, int expected, bool ended)
    {
        NativeBattleModulePoolTask task = new(1, 0, null, 0, new byte[100].ToImmutableArray());
        task = task.WriteWord(0x50, unchecked((ushort)old)).WriteWord(0x52, unchecked((ushort)duration));
        NativeBattleModulePoolTask next = NativeBattleLifeController.AdvanceSpriteFrame865230(task, out bool actual);
        Assert.Equal(ended, actual);
        Assert.Equal(unchecked((ushort)expected), next.ReadWord(0x50));
        Assert.Equal(ended ? 4 : 0, next.ReadByte(0x26) & 4);
        Assert.Equal(task.ReadWord(0x52), next.ReadWord(0x52));
    }

    [Theory]
    [InlineData(35)]
    [InlineData(5)]
    public void Producer_keeps_native_order_draw_counts_and_zeroed_unwritten_particle_padding(ushort effect)
    {
        var fixture = Fixture(effect);
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleLifeProfile profile = NativeBattleLifeProfile.ForEntry(fixture.Frame.Effect.Binding.Entry);
        uint root = hierarchy.Address(0, 0);
        NativeBattleEffectHierarchyAppend target = hierarchy.Append8DC540(1, profile.Code(0x864BC0), 100, root, (_, _, _) => new(0, 3));
        hierarchy = target.Hierarchy;
        hierarchy = hierarchy.Write(target.Address, hierarchy.Read(target.Address).WriteWord(0x24, 12).WriteWord(0x58, 100).WriteWord(0x5A, 64).WriteWord(0x5C, 1));
        NativeCrtRandomState random = fixture.Inputs.Random;
        hierarchy = NativeBattleLifeController.Produce(hierarchy, target.Address, fixture.Frame.Effect.Parameters!, fixture.Inputs.Packets, ref random);
        NativeCrtRandomState expected = fixture.Inputs.Random;
        //Five864DD0 draws, five864C80 jitter draws, one864F00 angle, three865050 wedge signs.
        for (int draw = 0; draw < 14; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, random);
        Assert.Equal(new[] { profile.Code(0x8653E0), profile.Code(0x8653E0), profile.Code(0x865270) }, hierarchy.Pools[3].Slots.Take(3).Select(task => task.Callback));
        Assert.Equal(3, hierarchy.Pools[4].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.All(hierarchy.Pools[3].Slots.Take(3), task =>
        {
            Assert.Equal((ushort)0, task.ReadWord(0x6E));
            Assert.Equal((byte)0, task.ReadByte(0x51));
        });
    }

    [Fact]
    public void Unknown_profile_and_corrupt_signed_phases_fail_without_a_generic_initializer_or_timer()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleLifeProfile.ForEntry(0x8649C1));
        var fixture = Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 255));
        NativeBattleLifeFrame corrupt = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        Assert.Throws<InvalidDataException>(() => NativeBattleLifeController.Advance864AB0(corrupt, fixture.Resources, fixture.Inputs));
    }

    private static (NativeBattleLifeFrame Frame, NativeBattleLifeResources Resources, NativeBattleLifeInputs Inputs) Fixture(ushort effectId = 35)
    {
        NativeBattleLifeProfile profile = NativeBattleLifeProfile.ForEntry(effectId == 35 ? 0x8649C0u : 0x8D4450u);
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 2, 11, 0, 24, effectId, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleLifeResources resources = new(null, new uint[] { 0x15E638C, 0x15E6538, 0x15E66E4, 0x15E6890, 0x15E68EC }.Select(address => Sprite(address + profile.StaticDelta)).ToImmutableArray(), 300021);
        NativeBattleOriginalEffectState effect = new(effectId, effectId - 1, new(effectId, profile.Entry, 0), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(profile.Global(0x26AC780), 0x10000)] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(0);
        byte[] tim = new byte[66];
        BinaryPrimitives.WriteUInt32LittleEndian(tim, 16);
        BinaryPrimitives.WriteUInt32LittleEndian(tim.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt32LittleEndian(tim.AsSpan(8), 44);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(18), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(tim.AsSpan(52), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(56), 64);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(58), 256);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(60), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(62), 1);
        memory = memory with { Arena = memory.Arena!.Write(0x10000, tim) };
        NativeBattleLifeFrame frame = NativeBattleLifeController.Initialize8649C0(memory, effect, packets, resources);
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), new byte[] { 1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0 }.ToImmutableArray());
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 10000, 4096);
        NativeBattleLifeInputs inputs = new(packets, new(camera, 216, 160, 108), index => new(index, 0) { SequenceState = new() { Pose = 0, PoseFrame114 = 1 }, Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, 4096) }, _ => source, NativeCrtRandomState.FromColdProcess(), (current, _) => current);
        return (frame, resources, inputs);
    }

    private static NativeBattleModuleResource Sprite(uint address)
    {
        const int count = 17;
        const int offset = 12 + 2 * count;
        byte[] bytes = new byte[offset + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), count);
        for (int index = 0; index <= count; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + 2 * index), offset);
        }
        return new(0, address, 0x570CC0, 0x570BC0, bytes.ToImmutableArray());
    }
}
