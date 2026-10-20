using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDrain39ControllerTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 255)]
    public void Entry_owns_five_actual_pools_header_actors_BYTE_maximum_and_resource_relative_banks(byte targets, byte maximum)
    {
        var fixture = Fixture(targets);
        Assert.Equal(new[] { 2, 4, 3, 3, 10 }, fixture.Frame.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal(new ushort[] { 100, 88, 60, 676, 64 }, fixture.Frame.Effect.Pools.Select(pool => pool.Stride));
        NativeBattleModulePoolTask root = fixture.Frame.Effect.Pools[0].Slots[0];
        Assert.Equal((byte)2, root.ReadByte(0x2C));
        Assert.Equal((byte)3, root.ReadByte(0x2D));
        Assert.Equal(maximum, root.ReadByte(0x2F));
        Assert.Equal(256 + 0xC000, fixture.Frame.State.AuxiliaryCursor);
        Assert.Null(fixture.Frame.State.PrimaryDrawCursor);
        Assert.Null(fixture.Frame.State.SecondaryDrawCursor);
        Assert.Null(fixture.Frame.State.CopiedCamera);
        Assert.Null(fixture.Frame.State.GeometryCursor);
    }

    [Fact]
    public void Phase_two_clears_actual_auxiliary_ranges_and_first_custom_child_has_zero_delay_and_duration_forty_five()
    {
        var fixture = Fixture();
        NativeBattleDrain39Frame frame = fixture.Frame;
        List<uint> callbacks = [];
        NativeBattleDrain39LeafStep Leaf(NativeBattleDrain39Frame current, uint address)
        {
            callbacks.Add(new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address).Callback);
            return new(current, 0);
        }
        for (int callback = 1; callback <= 4; callback++)
        {
            frame = NativeBattleDrain39Controller.Advance851510(frame, fixture.Inputs, Resources(), Leaf).Frame;
            Assert.Equal(256 + ((callback & 1) == 1 ? 0x6000 : 0), frame.State.PrimaryDrawCursor);
            Assert.Equal(256 + ((callback & 1) == 1 ? 0xC000 : 0x6000), frame.State.SecondaryDrawCursor);
        }
        Assert.Equal(new uint[] { 0x851710, 0x851A80, 0x851710 }, callbacks);
        Assert.Equal(256 + 0xC000, frame.State.Emitters);
        Assert.Equal(256 + 0xC000 + 0x10E0, frame.State.Geometry);
        Assert.Equal(256 + 0xC000 + 0x10E0 + 0x16F80, frame.State.AuxiliaryCursor);
        Assert.All(frame.Memory.Arena!.Read(frame.State.Emitters!.Value, 0x10E0 + 0x16F80), value => Assert.Equal((byte)0, value));
        NativeBattleModulePoolTask custom = Assert.Single(frame.Effect.Pools[3].Slots.Where(task => task.Callback == 0x851A80));
        Assert.Equal(0x15D5A90u, custom.ReadDword(0x30));
        Assert.Equal((ushort)0, custom.ReadWord(0x298));
        Assert.Equal((ushort)0, custom.ReadWord(0x29C));
        Assert.Equal((ushort)45, custom.ReadWord(0x29E));
        Assert.Equal(fixture.Inputs.Camera.Rotation.ToArray(), frame.State.CopiedCamera!.Rotation.ToArray());
    }

    [Fact]
    public void Child_counter_thirty_releases_the_root_and_next_phase_publishes_contact_with_exact_sound_once()
    {
        var fixture = Fixture();
        NativeBattleDrain39Frame frame = fixture.Frame;
        List<int> applied = [];
        List<NativeBattleModuleSound> sounds = [];
        NativeBattleDrain39ControllerInputs inputs = fixture.Inputs with
        {
            ApplyContact = (current, index) =>
            {
                applied.Add(index);
                return current;
            }
        };
        NativeBattleDrain39LeafStep Leaf(NativeBattleDrain39Frame current, uint _) => new(current, 0);
        for (int callback = 1; callback <= 35; callback++)
        {
            NativeBattleDrain39ControllerStep step = NativeBattleDrain39Controller.Advance851510(frame, inputs, Resources(), Leaf);
            sounds.AddRange(step.Sounds);
            Assert.Equal(callback == 35 ? new[] { 0 } : [], step.Contacts);
            frame = step.Frame;
        }
        Assert.Equal(new[] { 0 }, applied);
        NativeBattleModuleSound sound = Assert.Single(sounds);
        Assert.Equal(0x49433u, sound.Sound);
        Assert.Equal(0u, sound.Flags);
        Assert.Equal((byte)128, sound.Pan);
        Assert.Equal(127u, sound.Volume);
        NativeBattleDrain39Frame independent = JsonSerializer.Deserialize<NativeBattleDrain39Frame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleDrain39Controller.Advance851510(frame, fixture.Inputs, Resources(), Leaf)), JsonSerializer.Serialize(NativeBattleDrain39Controller.Advance851510(independent, fixture.Inputs, Resources(), Leaf)));
    }

    [Fact]
    public void Contact_memory_writes_reach_the_next_leaf_and_final_frame_without_restoring_preclear_bytes()
    {
        var fixture = Fixture();
        NativeBattleDrain39Frame frame = fixture.Frame;
        int contacts = 0;
        int leavesAfterContact = 0;
        NativeBattleDrain39ControllerInputs inputs = fixture.Inputs with
        {
            ApplyContact = (current, index) =>
            {
                Assert.Equal(0, index);
                Assert.Equal(0, contacts++);
                AssertCleared(current);
                NativeBattleEffectTextureState textures = current.Memory.Textures!;
                return current with { Memory = current.Memory with { Textures = textures.EnqueueCopy505EB0(1, 2, 3, 4, 5, 6) } };
            }
        };
        for (int callback = 0; callback < 35; callback++)
        {
            frame = NativeBattleDrain39Controller.Advance851510(frame, inputs, Resources(), (current, _) =>
            {
                AssertCleared(current);
                Assert.Equal(contacts, current.Memory.Textures!.Count);
                if (contacts != 0)
                {
                    leavesAfterContact++;
                }
                return new(current, 0);
            }).Frame;
        }
        Assert.Equal(1, contacts);
        Assert.True(leavesAfterContact > 0);
        AssertCleared(frame);
        NativeBattleEffectTimUpload copy = Assert.IsType<NativeBattleEffectTimUpload>(frame.Memory.Textures!.Slots[0]);
        Assert.Equal((byte)3, copy.Kind);
        Assert.Equal(new NativeBattleEffectVramCopy(1, 2, 3, 4, 5, 6), copy.Copy);
        NativeBattleDrain39Frame independent = JsonSerializer.Deserialize<NativeBattleDrain39Frame>(JsonSerializer.Serialize(frame))!;
        NativeBattleDrain39LeafStep Leaf(NativeBattleDrain39Frame current, uint _) => new(current, 0);
        NativeBattleDrain39ControllerStep actual = NativeBattleDrain39Controller.Advance851510(frame, inputs, Resources(), Leaf);
        NativeBattleDrain39ControllerStep restored = NativeBattleDrain39Controller.Advance851510(independent, inputs, Resources(), Leaf);
        Assert.Equal(JsonSerializer.Serialize(actual), JsonSerializer.Serialize(restored));
        Assert.Empty(actual.Contacts);
        Assert.Equal(1, actual.Frame.Memory.Textures!.Count);

        static void AssertCleared(NativeBattleDrain39Frame current)
        {
            int first = current.State.Emitters ?? throw new InvalidDataException("The current controller must clear its emitter pool before a private leaf or contact.");
            Assert.All(current.Memory.Arena!.Read(first, 0x10E0 + 0x16F80), value => Assert.Equal((byte)0, value));
        }
    }

    [Fact]
    public void Main_repeat_uses_signed_BYTE_group_and_signed_WORD_timer_instead_of_unsigned_waits()
    {
        var fixture = Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 4).WriteByte(0x63, 0).WriteByte(0x2A, 255));
        NativeBattleDrain39Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        NativeBattleDrain39LeafStep Leaf(NativeBattleDrain39Frame current, uint _) => new(current, 0);
        frame = NativeBattleDrain39Controller.Advance851510(frame, fixture.Inputs, Resources(), Leaf).Frame;
        Assert.Equal((byte)0, frame.Effect.Pools[0].Slots[0].ReadByte(0x2A));
        Assert.Equal((byte)1, frame.Effect.Pools[0].Slots[0].ReadByte(0x2E));
        Assert.Equal((byte)3, frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        hierarchy = new(frame.Effect.Pools);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 5).WriteWord(0x60, 0xFFFF));
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        frame = NativeBattleDrain39Controller.Advance851510(frame, fixture.Inputs, Resources(), Leaf).Frame;
        Assert.Equal((byte)6, frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        Assert.Contains(frame.Effect.Pools[4].Slots, task => task.Callback == 0x8568A0);
    }

    private static NativeBattleDrain39Resources Resources() => new(new("skipped-camera-registration", []), 0x49433, [], [], []);

    [Fact]
    public void Entry_skip_flag_preserves_unknown_texture_owner_and_non_skip_requires_the_actual_registration_owner()
    {
        var fixture = Fixture();
        NativeBattleEffectMemory memory = fixture.Frame.Memory with { Textures = null };
        NativeBattleDrain39Frame skipped = NativeBattleDrain39Controller.Initialize8513D0(memory, fixture.Frame.Effect, fixture.Inputs.Packets, Resources());
        Assert.Null(skipped.Memory.Textures);
        Assert.Equal(256, skipped.State.SourceArenaOffset);
        NativeBattleOriginalEffectState selected = fixture.Frame.Effect with { Parameters = fixture.Frame.Effect.Parameters! with { Flags79 = 0 } };
        Assert.Throws<NotSupportedException>(() => NativeBattleDrain39Controller.Initialize8513D0(memory, selected, fixture.Inputs.Packets, Resources()));
    }

    private static (NativeBattleDrain39Frame Frame, NativeBattleDrain39ControllerInputs Inputs) Fixture(byte targets = 1)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 11, 0, 23, 39, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(targets);
        NativeBattleOriginalEffectState selected = new(39, 38, new(39, 0x8513D0, 0x8513B0), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(0x269DA1C, 256)] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(38);
        memory = memory with { Arena = memory.Arena!.Write(256 + 0xC000, Enumerable.Repeat((byte)0xA5, 0x10E0 + 0x16F80).ToArray()) };
        NativeBattleDrain39Frame frame = NativeBattleDrain39Controller.Initialize8513D0(memory, selected, packets, Resources());
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), [1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0]);
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096);
        return (frame, new(packets, camera, index => new(index, 0) { SequenceState = new() { Pose = 0, PoseFrame114 = 1 } }, _ => source));
    }
}
