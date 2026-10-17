using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuragaControllerTests
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
        Assert.Null(fixture.Frame.State.Counter26D82E8);
        Assert.Equal(0, fixture.Frame.State.Counter26D82F8);
        Assert.Equal(0, fixture.Frame.State.Counter26D76C4);
    }

    [Fact]
    public void Phase_two_clears_actual_auxiliary_ranges_and_first_custom_child_has_zero_delay_and_duration_forty_five()
    {
        var fixture = Fixture();
        NativeBattleCuragaFrame frame = fixture.Frame;
        List<uint> callbacks = [];
        NativeBattleCuragaLeafStep Leaf(NativeBattleCuragaFrame current, uint address)
        {
            callbacks.Add(new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address).Callback);
            return new(current, 0);
        }
        for (int callback = 1; callback <= 4; callback++)
        {
            frame = NativeBattleCuragaController.Advance87DC50(frame, fixture.Inputs, Resources(), Leaf).Frame;
            Assert.Equal(256 + ((callback & 1) == 1 ? 0x6000 : 0), frame.State.PrimaryDrawCursor);
            Assert.Equal(256 + ((callback & 1) == 1 ? 0xC000 : 0x6000), frame.State.SecondaryDrawCursor);
        }
        Assert.Equal(new uint[] { 0x87DE50, 0x87E1A0, 0x87DE50 }, callbacks);
        Assert.Equal(256 + 0xC000, frame.State.Auxiliary25F8);
        Assert.Equal(256 + 0xC000 + 0x25F8, frame.State.Auxiliary16F80);
        Assert.Equal(256 + 0xC000 + 0x25F8 + 0x16F80, frame.State.AuxiliaryCursor);
        Assert.All(frame.Memory.Arena!.Read(frame.State.Auxiliary25F8!.Value, 0x25F8 + 0x16F80), value => Assert.Equal((byte)0, value));
        NativeBattleModulePoolTask custom = Assert.Single(frame.Effect.Pools[3].Slots.Where(task => task.Callback == 0x87E1A0));
        Assert.Equal(0x17D5F4Cu, custom.ReadDword(0x30));
        Assert.Equal((ushort)0, custom.ReadWord(0x298));
        Assert.Equal((ushort)0, custom.ReadWord(0x29C));
        Assert.Equal((ushort)45, custom.ReadWord(0x29E));
        Assert.Equal(fixture.Inputs.Camera.Rotation.ToArray(), frame.State.CopiedCamera!.Rotation.ToArray());
    }

    [Fact]
    public void Child_counter_thirty_releases_the_root_and_next_phase_publishes_contact_with_exact_sound_once()
    {
        var fixture = Fixture();
        NativeBattleCuragaFrame frame = fixture.Frame;
        List<int> applied = [];
        List<NativeBattleModuleSound> sounds = [];
        NativeBattleCuragaControllerInputs inputs = fixture.Inputs with { ApplyContact = applied.Add };
        NativeBattleCuragaLeafStep Leaf(NativeBattleCuragaFrame current, uint _) => new(current, 0);
        for (int callback = 1; callback <= 35; callback++)
        {
            NativeBattleCuragaControllerStep step = NativeBattleCuragaController.Advance87DC50(frame, inputs, Resources(), Leaf);
            sounds.AddRange(step.Sounds);
            Assert.Equal(callback == 35 ? new[] { 0 } : [], step.Contacts);
            frame = step.Frame;
        }
        Assert.Equal(new[] { 0 }, applied);
        NativeBattleModuleSound sound = Assert.Single(sounds);
        Assert.Equal(0x4946Fu, sound.Sound);
        Assert.Equal(0u, sound.Flags);
        Assert.Equal((byte)128, sound.Pan);
        Assert.Equal(127u, sound.Volume);
        NativeBattleCuragaFrame independent = JsonSerializer.Deserialize<NativeBattleCuragaFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleCuragaController.Advance87DC50(frame, fixture.Inputs, Resources(), Leaf)), JsonSerializer.Serialize(NativeBattleCuragaController.Advance87DC50(independent, fixture.Inputs, Resources(), Leaf)));
    }

    [Fact]
    public void Main_repeat_uses_signed_BYTE_group_and_signed_WORD_timer_instead_of_unsigned_waits()
    {
        var fixture = Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 4).WriteByte(0x63, 0).WriteByte(0x2A, 255));
        NativeBattleCuragaFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        NativeBattleCuragaLeafStep Leaf(NativeBattleCuragaFrame current, uint _) => new(current, 0);
        frame = NativeBattleCuragaController.Advance87DC50(frame, fixture.Inputs, Resources(), Leaf).Frame;
        Assert.Equal((byte)0, frame.Effect.Pools[0].Slots[0].ReadByte(0x2A));
        Assert.Equal((byte)1, frame.Effect.Pools[0].Slots[0].ReadByte(0x2E));
        Assert.Equal((byte)3, frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        hierarchy = new(frame.Effect.Pools);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 5).WriteWord(0x60, 0xFFFF));
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        frame = NativeBattleCuragaController.Advance87DC50(frame, fixture.Inputs, Resources(), Leaf).Frame;
        Assert.Equal((byte)6, frame.Effect.Pools[0].Slots[0].ReadByte(0x29));
        Assert.Contains(frame.Effect.Pools[4].Slots, task => task.Callback == 0x882FC0);
    }

    private static NativeBattleCuragaResources Resources() => new(new("skipped-camera-registration", []), 0x4946F);

    private static (NativeBattleCuragaFrame Frame, NativeBattleCuragaControllerInputs Inputs) Fixture(byte targets = 1)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 11, 0, 23, 28, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(targets);
        NativeBattleOriginalEffectState selected = new(28, 27, new(28, 0x87DB10, 0x87DAF0), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(0x26D75BC, 256)] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(27);
        memory = memory with { Arena = memory.Arena!.Write(256 + 0xC000, Enumerable.Repeat((byte)0xA5, 0x25F8 + 0x16F80).ToArray()) };
        NativeBattleCuragaFrame frame = NativeBattleCuragaController.Initialize87DB10(memory, selected, packets, Resources());
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), [1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0]);
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096);
        return (frame, new(packets, camera, index => new(index, 0) { SequenceState = new() { Pose = 0, PoseFrame114 = 1 } }, _ => source));
    }
}
