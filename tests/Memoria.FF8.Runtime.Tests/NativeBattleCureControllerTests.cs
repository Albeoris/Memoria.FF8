using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCureControllerTests
{
    [Fact]
    public void Entry_initializes_the_four_original_banks_and_unsigned_target_maximum_without_reading_skipped_texture_state()
    {
        var fixture = Fixture(0, 2);
        NativeBattleCureFrame frame = fixture.Frame;
        Assert.Equal(0x277AFC0u, frame.Effect.ReturnedDescriptor);
        Assert.Equal(new[] { 2, 4, 300, 100 }, frame.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal(new ushort[] { 100, 88, 108, 92 }, frame.Effect.Pools.Select(pool => pool.Stride));
        NativeBattleModulePoolTask root = frame.Effect.Pools[0].Slots[0];
        Assert.Equal(0x1D99A78u, root.ReadDword(0x0C));
        Assert.Equal((byte)0, root.ReadByte(0x2C));
        Assert.Equal((byte)3, root.ReadByte(0x2D));
        Assert.Equal((byte)255, root.ReadByte(0x2F));
        Assert.Equal((ushort)0, root.ReadWord(0x5A));
        Assert.Equal((ushort)1, root.ReadWord(0x58));
        Assert.Null(frame.Memory.Textures);
        Assert.Null(frame.State.PrimaryDrawCursor);
        Assert.Null(frame.State.CopiedCamera);
        Assert.Equal((byte)2, Fixture(3).Frame.Effect.Pools[0].Slots[0].ReadByte(0x2F));
        Assert.Equal((byte)4, Fixture(1, 5).Frame.Effect.Pools[0].Slots[0].ReadByte(0x2F));
    }

    [Fact]
    public void Controller_uses_same_pass_children_the_exact_contact_counter_and_the_native_unpositioned_sound()
    {
        var fixture = Fixture();
        NativeBattleCureFrame frame = fixture.Frame;
        List<int> applied = [];
        NativeBattleCureControllerInputs inputs = fixture.Inputs with { ApplyContact = applied.Add };
        int leaves = 0;
        List<int> contacts = [];
        List<NativeBattleModuleSound> sounds = [];
        for (int callback = 1; callback <= 19; callback++)
        {
            NativeBattleCureControllerStep step = NativeBattleCureController.Advance8D6B30(frame, inputs, Resources(), (current, address) =>
            {
                Assert.Equal(0x8D6D80u, new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address).Callback);
                leaves++;
                return new(current, 0);
            });
            frame = step.Frame;
            contacts.AddRange(step.Contacts);
            sounds.AddRange(step.Sounds);
            if (callback < 4)
            {
                Assert.Equal(0, leaves);
            }
            if (callback < 19)
            {
                Assert.Empty(contacts);
            }
            Assert.Equal(callback % 2 == 1 ? 0x10000 : 0, frame.State.PrimaryDrawCursor);
            Assert.Equal(callback % 2 == 1 ? 0x20000 : 0x10000, frame.State.SecondaryDrawCursor);
            Assert.Equal(inputs.Camera, frame.State.CopiedCamera);
        }
        Assert.Equal(16, leaves);
        Assert.Equal(new[] { 0 }, contacts);
        Assert.Equal(contacts, applied);
        NativeBattleModuleSound sound = Assert.Single(sounds);
        Assert.Equal(0x493F2u, sound.Sound);
        Assert.Equal(0u, sound.Flags);
        Assert.Equal((byte?)128, sound.Pan);
        Assert.Equal(127u, sound.Volume);
        Assert.Null(sound.Anchor);
        Assert.Equal((byte)4, frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        Assert.Equal((byte)1, frame.Effect.Pools[1].Slots[0].ReadByte(0x28));
    }

    [Fact]
    public void Main_wait_uses_the_previous_survivor_count_and_completion_removes_the_actual_descriptor_owner()
    {
        var fixture = Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 6).WriteWord(0x5E, 1));
        NativeBattleCureFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        NativeBattleCureLeafStep Unreached(NativeBattleCureFrame _, uint __) => throw new InvalidOperationException("No native child exists.");
        frame = NativeBattleCureController.Advance8D6B30(frame, fixture.Inputs, Resources(), Unreached).Frame;
        Assert.Equal((byte)6, frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        Assert.Equal((ushort)0, frame.Effect.Pools[0].Slots[0].ReadWord(0x5E));
        frame = NativeBattleCureController.Advance8D6B30(frame, fixture.Inputs, Resources(), Unreached).Frame;
        Assert.Equal((byte)7, frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        hierarchy = new(frame.Effect.Pools);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 9));
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        NativeBattleCureControllerStep finished = NativeBattleCureController.Advance8D6B30(frame, fixture.Inputs, Resources(), Unreached);
        Assert.Equal(0u, finished.Frame.Effect.ReturnedDescriptor);
        Assert.Null(finished.Frame.Effect.Pools[0].Head);
        Assert.Equal((ushort)0, finished.Frame.Effect.Pools[0].Slots[0].Flags);
        Assert.Equal((byte)10, finished.Frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        Assert.Same(finished.Frame, NativeBattleCureController.Advance8D6B30(finished.Frame, fixture.Inputs, Resources(), Unreached).Frame);
    }

    [Fact]
    public void Independent_JSON_controller_restores_keep_child_links_contact_timing_and_buffer_parity()
    {
        var fixture = Fixture();
        NativeBattleCureFrame frame = fixture.Frame;
        NativeBattleCureLeafStep KeepLeaf(NativeBattleCureFrame current, uint _) => new(current, 0);
        for (int callback = 0; callback < 4; callback++)
        {
            frame = NativeBattleCureController.Advance8D6B30(frame, fixture.Inputs, Resources(), KeepLeaf).Frame;
        }
        string json = JsonSerializer.Serialize(frame);
        NativeBattleCureFrame first = JsonSerializer.Deserialize<NativeBattleCureFrame>(json)!;
        NativeBattleCureFrame second = JsonSerializer.Deserialize<NativeBattleCureFrame>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(first));
        for (int callback = 0; callback < 17; callback++)
        {
            NativeBattleCureControllerStep left = NativeBattleCureController.Advance8D6B30(first, fixture.Inputs, Resources(), KeepLeaf);
            NativeBattleCureControllerStep right = NativeBattleCureController.Advance8D6B30(second, fixture.Inputs, Resources(), KeepLeaf);
            Assert.Equal(JsonSerializer.Serialize(left), JsonSerializer.Serialize(right));
            first = left.Frame;
            second = right.Frame;
        }
    }

    private static (NativeBattleCureFrame Frame, NativeBattleCureControllerInputs Inputs) Fixture(byte targets = 1, byte repeatMode = 1)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 2, 11, 0, 21, 1, null));
        for (int target = 0; target < Math.Max(1, (int)targets); target++)
        {
            (packets, _) = packets.AppendContact(new(new((byte)(3 + target), 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        }
        packets = packets.WritePlayerRepeatMode(0, repeatMode);
        (packets, _) = packets.Publish(targets);
        NativeBattleEffectParameters parameters = new(0, 1, 8, 0, 0, 0);
        NativeBattleOriginalEffectState effect = new(1, 0, new(1, 0x8D6A00, 0x8D69E0), parameters, null) { ResourcePointers = [new(0x277AEC0, 0)] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(0) with { Textures = null };
        NativeBattleCureFrame frame = NativeBattleCureController.Initialize8D6A00(memory, effect, packets, Resources());
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), new byte[] { 1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0 }.ToImmutableArray());
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 1000, 4096);
        NativeBattleCureControllerInputs inputs = new(packets, camera, index => new(index, 0) { SequenceState = new() { Pose = 0, PoseFrame114 = 1 } }, _ => source);
        return (frame, inputs);
    }

    private static NativeBattleCureResources Resources() => new(new("synthetic-controller-animation-unused", []), [], 0x493F2);
}
