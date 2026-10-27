using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuraControllerTests
{
    [Fact]
    public void Entry_keeps_its_actual_bank_layout_first_contact_and_cleared_fields_without_inventing_Cure_initializers()
    {
        var fixture = Fixture(2);
        Assert.Equal(new[] { 2, 4, 64, 500 }, fixture.Frame.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal(new ushort[] { 100, 88, 200, 132 }, fixture.Frame.Effect.Pools.Select(pool => pool.Stride));
        NativeBattleModulePoolTask root = fixture.Frame.Effect.Pools[0].Slots[0];
        Assert.Equal(0x1D99A78u, root.ReadDword(0x0C));
        Assert.Equal((byte)3, root.ReadByte(0x2D));
        Assert.Equal((byte)0, root.ReadByte(0x2C));
        Assert.Equal((byte)0, root.ReadByte(0x2F));
        Assert.Equal((ushort)1, root.ReadWord(0x5A));
        Assert.Equal((ushort)0, root.ReadWord(0x58));
        Assert.Null(fixture.Frame.Memory.Textures);
        Assert.Null(fixture.Frame.State.PrimaryDrawCursor);
        Assert.Equal(0, fixture.Frame.State.OddDrawBase26FD114);
        Assert.Equal(0x12000, fixture.Frame.State.EvenDrawBase26FD110);
    }

    [Fact]
    public void Same_pass_dispatch_preserves_private_pool_order_and_exact_C4_angles_and_alternates_literal_arena_banks()
    {
        var fixture = Fixture();
        List<uint> callbacks = [];
        NativeBattleCuraLeafStep Leaf(NativeBattleCuraFrame current, uint address)
        {
            callbacks.Add(new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address).Callback);
            return new(current, 0);
        }
        NativeBattleCuraFrame frame = NativeBattleCuraController.Advance889E40(fixture.Frame, fixture.Inputs, Resources(), Leaf).Frame;
        Assert.Empty(callbacks);
        Assert.Equal(0x12000, frame.State.PrimaryDrawCursor);
        frame = NativeBattleCuraController.Advance889E40(frame, fixture.Inputs, Resources(), Leaf).Frame;
        Assert.Equal(0, frame.State.PrimaryDrawCursor);
        Assert.Equal(new uint[] { 0x88A740, 0x88B530, 0x88B810, 0x88B3D0, 0x88A070 }, callbacks);
        Assert.Equal((ushort)0, frame.Effect.Pools[2].Slots[0].ReadWord(0xC4));
        callbacks.Clear();
        frame = NativeBattleCuraController.Advance889E40(frame, fixture.Inputs, Resources(), Leaf).Frame;
        Assert.Equal(new uint[] { 0x88A740, 0x88B530, 0x88B810, 0x88B3D0, 0x88A740, 0x88BBD0, 0x88A070 }, callbacks);
        Assert.Equal((ushort)0x800, frame.Effect.Pools[2].Slots[4].ReadWord(0xC4));
        Assert.Equal(0x12000, frame.State.PrimaryDrawCursor);
    }

    [Fact]
    public void Contact_uses_signed_greater_than_thirty_and_preserves_exact_sound_and_json_next_callback()
    {
        var fixture = Fixture();
        NativeBattleCuraFrame frame = fixture.Frame;
        List<int> applied = [];
        List<NativeBattleModuleSound> sounds = [];
        NativeBattleCuraControllerInputs inputs = fixture.Inputs with { ApplyContact = applied.Add };
        NativeBattleCuraLeafStep Leaf(NativeBattleCuraFrame current, uint _) => new(current, 0);
        for (int callback = 1; callback <= 33; callback++)
        {
            NativeBattleCuraControllerStep step = NativeBattleCuraController.Advance889E40(frame, inputs, Resources(), Leaf);
            sounds.AddRange(step.Sounds);
            if (callback < 33)
            {
                Assert.Empty(step.Contacts);
                Assert.Empty(applied);
            }
            else
            {
                Assert.Equal(new[] { 0 }, step.Contacts);
                Assert.Equal(new[] { 0 }, applied);
            }
            frame = step.Frame;
        }
        NativeBattleModuleSound sound = Assert.Single(sounds);
        Assert.Equal(0x4942Du, sound.Sound);
        Assert.Equal(0u, sound.Flags);
        Assert.Equal((byte)128, sound.Pan);
        Assert.Equal(127u, sound.Volume);
        Assert.Null(sound.Anchor);
        NativeBattleCuraFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleCuraFrame>(NativeBattleTestSnapshots.Json(frame))!;
        var next = NativeBattleCuraController.Advance889E40(frame, fixture.Inputs, Resources(), Leaf);
        var independent = NativeBattleCuraController.Advance889E40(restored, fixture.Inputs, Resources(), Leaf);
        Assert.Equal(NativeBattleTestSnapshots.Json(next), NativeBattleTestSnapshots.Json(independent));
        Assert.Empty(next.Contacts);
    }

    [Fact]
    public void Main_group_comparison_is_signed_and_the_survivor_wait_reads_the_previous_count()
    {
        var fixture = Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 2).WriteByte(0x63, 0).WriteByte(0x2A, 255).WriteWord(0x58, 0));
        NativeBattleCuraFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        NativeBattleCuraLeafStep Unreached(NativeBattleCuraFrame _, uint __) => throw new InvalidOperationException("No child was appended.");
        frame = NativeBattleCuraController.Advance889E40(frame, fixture.Inputs, Resources(), Unreached).Frame;
        Assert.Equal((byte)0, frame.Effect.Pools[0].Slots[0].ReadByte(0x2A));
        Assert.Equal((byte)1, frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        hierarchy = new(frame.Effect.Pools);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 3).WriteWord(0x5E, 1));
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        frame = NativeBattleCuraController.Advance889E40(frame, fixture.Inputs, Resources(), Unreached).Frame;
        Assert.Equal((byte)3, frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        Assert.Equal((ushort)0, frame.Effect.Pools[0].Slots[0].ReadWord(0x5E));
        frame = NativeBattleCuraController.Advance889E40(frame, fixture.Inputs, Resources(), Unreached).Frame;
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
    }

    private static NativeBattleCuraResources Resources() => new(new("unused-skipped-registration", []), [], 0x4942D);

    private static (NativeBattleCuraFrame Frame, NativeBattleCuraControllerInputs Inputs) Fixture(byte actor = 0)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        int headerIndex;
        (packets, headerIndex) = packets.Prepare(new(actor, 2, 11, 0, 22, 1, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(headerIndex, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState selected = new(25, 24, new(25, 0x889D60, 0x889D40), new(actor, 1, 8, headerIndex, 0, 0), null);
        NativeBattleCuraFrame frame = NativeBattleCuraController.Initialize889D60(NativeBattleEffectMemory.FromColdProcess() with { Textures = null }, selected, packets, Resources());
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), [1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0]);
        return (frame, new(packets, index => new(index, 0) { SequenceState = new() { Pose = 0, PoseFrame114 = 1 } }, _ => source));
    }
}
