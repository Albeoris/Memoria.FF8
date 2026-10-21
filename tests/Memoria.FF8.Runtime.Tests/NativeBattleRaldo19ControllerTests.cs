using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRaldo19ControllerTests
{
    [Fact]
    public void Entry_preserves_mutable_blocks_and_uses_actual_six_pools_and_4000_banks()
    {
        var fixture = Fixture();
        Assert.Equal(new ushort[] { 100, 88, 72, 676, 184, 64 }, fixture.Frame.Effect.Pools.Select(pool => pool.Stride));
        Assert.Equal(new[] { 2, 4, 3, 1, 50, 10 }, fixture.Frame.Effect.Pools.Select(pool => pool.Slots.Length));
        NativeBattleRaldo19State retained = fixture.Frame.State with { BallC4 = fixture.Frame.State.BallC4.MarkUnknown(0xA4, 2), Descriptor48 = fixture.Frame.State.Descriptor48.Write(44, new byte[] { 1, 2, 3, 4 }), Counter273048C = 123, Counter272DDDC = 456 };
        NativeBattleRaldo19Frame initialized = NativeBattleRaldo19Controller.Initialize(fixture.Frame.Memory, fixture.Frame.Effect, fixture.Frame.Random, fixture.Inputs.Packets, fixture.Resources, retained);
        Assert.All(initialized.State.BallC4.ReadSnapshot(0xA4, 2).Known, known => Assert.False(known));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, initialized.State.Descriptor48.Read(44, 4));
        Assert.Equal(0u, initialized.State.Counter273048C);
        Assert.Equal(0u, initialized.State.Counter272DDDC);
        NativeBattleRaldo19Frame frame = NativeBattleRaldo19Controller.Advance(initialized, fixture.Resources, fixture.Inputs);
        Assert.Equal(256 + 0x4000, frame.State.Nodes!.PrimaryDrawCursor);
        Assert.Equal(256 + 0x8000, frame.State.Nodes.SecondaryDrawCursor);
        Assert.Equal(256 + 0x8000, frame.State.Nodes.AuxiliaryCursor);
        frame = NativeBattleRaldo19Controller.Advance(frame, fixture.Resources, fixture.Inputs);
        frame = NativeBattleRaldo19Controller.Advance(frame, fixture.Resources, fixture.Inputs);
        Assert.Equal(256 + 0x8000 + 0x438, frame.State.Nodes!.Auxiliary16F80);
        Assert.Equal(256 + 0x8000 + 0x438 + 0xB7C0, frame.State.Nodes.AuxiliaryCursor);
    }

    [Fact]
    public void Mode4_capture_preserves_the_full_second_DWORD_and_forces_only_the_Y_WORD()
    {
        var fixture = Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend generic = hierarchy.Append8DC540(3, 0x8A3A30, 676, hierarchy.Address(0, 0), (_, _, _) => new(0, 3));
        NativeBattleModulePoolTask task = generic.Hierarchy.Read(generic.Address).WriteWord(0x29C, 4).WriteWord(0x296, 0x4321);
        NativeBattleGraphicsModel model = new(0, 0) { Transform = new(new(0, 0, 0), new(-123, 456, -789), unchecked((short)0xABCD), 0, 4096) };
        task = NativeBattleRaldo19Controller.PrepareMode4Caster(task, model);
        Assert.Equal((short)-123, unchecked((short)task.ReadWord(0x290)));
        Assert.Equal((short)-768, unchecked((short)task.ReadWord(0x292)));
        Assert.Equal((short)-789, unchecked((short)task.ReadWord(0x294)));
        Assert.Equal((ushort)0xABCD, task.ReadWord(0x296));
        Assert.Throws<InvalidDataException>(() => NativeBattleRaldo19Controller.PrepareMode4Caster(task.WriteWord(0x29C, 0), model));
    }

    [Fact]
    public void Director_spawns_mode4_without_contact_or_a_request_two_and_releases_only_on_native_hit_flags()
    {
        var fixture = Fixture();
        List<NativeBattleRaldo19Operation> operations = [];
        NativeBattleRaldo19Inputs inputs = fixture.Inputs with { Execute = (frame, operation) =>
        {
            operations.Add(operation);
            return frame;
        } };
        NativeBattleRaldo19Frame current = fixture.Frame;
        for (int callback = 1; callback <= 53; callback++)
        {
            current = NativeBattleRaldo19Controller.Advance(current, fixture.Resources, inputs);
        }
        Assert.Empty(operations.OfType<NativeBattleRaldo19Contact>());
        Assert.Equal((short)0, current.State.Word(0x48));
        Assert.Equal((short)1, current.State.Word(0x44));
        NativeBattleModulePoolTask generic = Assert.Single(current.Effect.Pools[3].Slots, task => task.Callback == 0x8A3A30);
        Assert.Equal((ushort)28, generic.ReadWord(0x298));
        Assert.Equal((ushort)4, generic.ReadWord(0x29C));
        Assert.Equal((ushort)0, generic.ReadWord(0x29E));
        Assert.Single(current.Effect.Pools[4].Slots, task => task.Callback == 0x8A93E0);
        NativeBattleRaldo19Frame restored = JsonSerializer.Deserialize<NativeBattleRaldo19Frame>(JsonSerializer.Serialize(current))!;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleRaldo19Controller.Advance(current, fixture.Resources, fixture.Inputs)), JsonSerializer.Serialize(NativeBattleRaldo19Controller.Advance(restored, fixture.Resources, fixture.Inputs)));
        current = current with { State = current.State.WithWord(0x48, 1) };
        current = NativeBattleRaldo19Controller.Advance(current, fixture.Resources, inputs);
        Assert.Single(operations.OfType<NativeBattleRaldo19Contact>());
        current = NativeBattleRaldo19Controller.Advance(current, fixture.Resources, inputs);
        Assert.Equal((byte)1, current.Effect.Pools[0].Slots[0].ReadByte(0x63));
        current = current with { State = current.State.WithWord(0x4C, 1) };
        current = NativeBattleRaldo19Controller.Advance(current, fixture.Resources, inputs);
        current = NativeBattleRaldo19Controller.Advance(current, fixture.Resources, inputs);
        Assert.Equal((byte)0, current.Effect.Pools[0].Slots[0].ReadByte(0x63));
        Assert.Single(operations.OfType<NativeBattleRaldo19Contact>());
    }

    [Fact]
    public void Actor_selection_keeps_the_last_enabled_70_identity_with_equal_X_Y_and_Z()
    {
        var fixture = Fixture();
        NativeBattleRaldo19Inputs inputs = fixture.Inputs with { ReadModel = index =>
        {
            NativeBattleGraphicsModel model = fixture.Inputs.ReadModel(index);
            return index switch
            {
                2 or 4 => model with { Flags0 = 2, ModelId4 = 0x70 },
                5 => model with { Flags0 = 2, ModelId4 = 0x70, Transform = model.Transform! with { Position28 = new(0, 1, 0) } },
                _ => model
            };
        } };
        NativeBattleRaldo19Frame current = fixture.Frame;
        for (int callback = 1; callback <= 6; callback++)
        {
            current = NativeBattleRaldo19Controller.Advance(current, fixture.Resources, inputs);
        }
        NativeBattleModulePoolTask ball = Assert.Single(current.Effect.Pools[4].Slots, task => task.Callback == 0x8A93E0);
        Assert.Equal((byte)4, ball.ReadByte(0x2C));
    }

    internal static (NativeBattleRaldo19Frame Frame, NativeBattleRaldo19Resources Resources, NativeBattleRaldo19Inputs Inputs) Fixture()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 11, 0, 32, 15, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        byte[] cameraBytes = new byte[0x26C];
        BinaryPrimitives.WriteUInt16LittleEndian(cameraBytes, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(cameraBytes.AsSpan(6), (ushort)cameraBytes.Length);
        NativeBattleRaldo19Resources resources = new(new("camera", cameraBytes.ToImmutableArray()), new(new byte[0x4760].ToImmutableArray()) { ResourceAddress = 0x17F37BC }, 0x64254, new(0x162A470, [0x404040, 0xFF000000]), Sprite(10), Sprite(12), new byte[84].ToImmutableArray(), new byte[0xC4].ToImmutableArray(), new byte[0x4E30].ToImmutableArray(), new byte[48].ToImmutableArray(), new byte[0x1F40].ToImmutableArray(), 0, 0);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(18);
        memory = memory with { Arena = memory.Arena!.Write(256 + 0x8000, Enumerable.Repeat((byte)0xA5, 0x438 + 0xB7C0).ToArray()) };
        NativeBattleOriginalEffectState selected = new(19, 18, new(19, 0x8A32A0, 0x8A3280), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(0x2726900, 256)] };
        NativeBattleRaldo19Frame frame = NativeBattleRaldo19Controller.Initialize(memory, selected, new(1), packets, resources, NativeBattleRaldo19State.FromColdProcess(resources));
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), [1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0]);
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096);
        NativeBattleRaldo19Inputs inputs = new(packets, camera, index => new(index, 0) { Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, 4096), SequenceState = new() { Pose = 0, PoseFrame114 = 1 } }, _ => source, (current, _) => current, (current, _) => new(current, 0));
        return (frame, resources, inputs);
    }

    private static ImmutableArray<byte> Sprite(int count)
    {
        int header = 12 + 2 * count;
        byte[] bytes = new byte[header + count * 24];
        bytes[3] = 6;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), (ushort)count);
        for (int frame = 0; frame < count; frame++)
        {
            int offset = header + frame * 24;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + frame * 2), (ushort)offset);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), 1);
            bytes[offset + 4] = 1;
            bytes[offset + 6] = 1;
            bytes[offset + 7] = 0x2C;
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + 2 * count), ushort.MaxValue);
        return bytes.ToImmutableArray();
    }
}
