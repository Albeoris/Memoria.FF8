using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSiren95ControllerTests
{
    private const uint Root = NativeBattleSiren95Controller.MainBank;
    private const uint Base = NativeBattleEffectNodeMemory.ArenaAddress;

    [Theory]
    [InlineData(0, 1, 255)]
    [InlineData(3, 1, 2)]
    [InlineData(1, 5, 4)]
    public void Entry_uses_ten_physical_banks_and_unsigned_wrapped_contact_limit(byte targets, byte repeats, byte last)
    {
        var fixture = Fixture(targets, repeats);
        NativeBattleSiren95Frame frame = fixture.Frame;
        Assert.Equal(new[] { 2, 4, 3, 10, 8, 110, 60, 10, 1, 2 }, frame.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal(new ushort[] { 0x64, 0x58, 0x48, 0x2A4, 0x534, 0xE0, 0x64, 0x40, 0x140, 0x30 }, frame.Effect.Pools.Select(pool => pool.Stride));
        NativeBattleModulePoolTask root = frame.Effect.Pools[0].Slots[0];
        Assert.Equal(0x1D99A78u, root.ReadDword(0xC));
        Assert.Equal((byte)0, root.ReadByte(0x2C));
        Assert.Equal((byte)3, root.ReadByte(0x2D));
        Assert.Equal(last, root.ReadByte(0x2F));
        Assert.Equal((ushort)targets, root.ReadWord(0x5A));
        Assert.Equal((ushort)(repeats - 1), root.ReadWord(0x58));
        Assert.Equal(Base + 0x10000, frame.Memory.Siren95!.ReadDword(0x258BFA4));
        Assert.Equal(Base + 0x10C00, frame.Memory.Siren95.ReadDword(0x258BFA0));
        Assert.Equal(Base + 0x11800, frame.Memory.Siren95.ReadDword(0x258FB74));
        Assert.Equal(0x12345678u, frame.Memory.Siren95.ReadDword(0x258FCF0));
    }

    [Fact]
    public void Master_wait_reads_previous_survivors_preserves_raw_camera_padding_and_wraps_clocks()
    {
        var fixture = Fixture();
        NativeBattleSiren95Frame frame = WriteRoot(fixture.Frame, task => task.WriteByte(0x29, 6).WriteWord(0x5E, 1).WriteWord(0x5C, ushort.MaxValue).WriteWord(0x24, ushort.MaxValue));
        frame = NativeBattleSiren95Controller.Advance739F40(frame, fixture.Inputs);
        Assert.Equal((byte)6, RootTask(frame).ReadByte(0x29));
        Assert.Equal((ushort)0, RootTask(frame).ReadWord(0x5E));
        Assert.Equal((ushort)0, RootTask(frame).ReadWord(0x5C));
        Assert.Equal((ushort)0, RootTask(frame).ReadWord(0x24));
        Assert.Equal(Base + 0x10000, frame.Memory.Siren95!.ReadDword(0x257F8A4));
        Assert.Equal(Base + 0x10C00, frame.Memory.Siren95.ReadDword(0x257F9AC));
        Assert.Equal(fixture.Inputs.CameraMatrix.Bytes, frame.Memory.SharedEffectView2793E58!.Bytes);
        Assert.False(frame.Memory.SharedEffectView2793E58.Known[18]);
        Assert.False(frame.Memory.SharedEffectView2793E58.Known[19]);
        frame = NativeBattleSiren95Controller.Advance739F40(frame, fixture.Inputs);
        Assert.Equal((byte)7, RootTask(frame).ReadByte(0x29));
        Assert.Equal(Base + 0x10C00, frame.Memory.Siren95!.ReadDword(0x257F8A4));
        Assert.Equal(Base + 0x11800, frame.Memory.Siren95.ReadDword(0x257F9AC));
    }

    [Fact]
    public void Resource_completion_keeps_actual_pointer_mutations_and_waits_for_its_completion_byte()
    {
        var fixture = Fixture();
        NativeBattleSiren95Frame frame = WriteRoot(fixture.Frame, task => task.WriteByte(0x29, 7));
        List<NativeBattleChoco97Operation> operations = [];
        NativeBattleSiren95Inputs inputs = fixture.Inputs with
        {
            Execute = (current, operation) =>
            {
                operations.Add(operation);
                return current;
            }
        };
        frame = NativeBattleSiren95Controller.Advance739F40(frame, inputs);
        NativeBattleChoco97CommonTextureLoad load = Assert.IsType<NativeBattleChoco97CommonTextureLoad>(Assert.Single(operations));
        Assert.Equal(0x10000, load.DestinationArenaOffset);
        Assert.Equal(Root + 0x62, load.CompletionAddress);
        Assert.Equal((byte)8, RootTask(frame).ReadByte(0x29));
        frame = NativeBattleSiren95Controller.Advance739F40(frame, inputs);
        Assert.Equal((byte)8, RootTask(frame).ReadByte(0x29));
        frame = frame with { Effect = NativeBattleSiren95Controller.WriteCompletionByte(frame.Effect, load.CompletionAddress, 1) };
        frame = NativeBattleSiren95Controller.Advance739F40(frame, inputs);
        Assert.Equal((byte)9, RootTask(frame).ReadByte(0x29));
        frame = NativeBattleSiren95Controller.Advance739F40(frame, inputs);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Null(frame.Effect.Pools[0].Head);
        Assert.Throws<NotSupportedException>(() => NativeBattleSiren95Controller.WriteCompletionByte(fixture.Frame.Effect, Root + 0x61, 1));
    }

    [Fact]
    public void First_workspace_initialization_clears_actual_current_arena_spans_and_preserves_unwritten_tail()
    {
        var fixture = Fixture();
        NativeBattleSiren95Frame frame = WriteRoot(fixture.Frame, task => task.WriteByte(0x29, 2));
        int first = 0x11800;
        int length = 0x10E0 + 0xC240;
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(first, Enumerable.Repeat((byte)0xA5, length + 1).ToArray()) } };
        frame = NativeBattleSiren95Controller.Advance739F40(frame, fixture.Inputs);
        Assert.Equal((byte)3, RootTask(frame).ReadByte(0x29));
        Assert.Equal(Base + (uint)first, frame.Memory.Siren95!.ReadDword(0x258EC54));
        Assert.Equal(Base + (uint)first + 0x10E0, frame.Memory.Siren95.ReadDword(0x258FB6C));
        Assert.Equal(Base + (uint)(first + length), frame.Memory.Siren95.ReadDword(0x258FB74));
        Assert.All(frame.Memory.Arena!.Read(first, length), value => Assert.Equal((byte)0, value));
        Assert.Equal((byte)0xA5, frame.Memory.Arena.Read(first + length, 1)[0]);
        string saved = JsonSerializer.Serialize(frame);
        NativeBattleSiren95Frame restored = JsonSerializer.Deserialize<NativeBattleSiren95Frame>(saved)!;
        Assert.Equal(saved, JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Damage_phase_applies_every_unsigned_header_contact_and_preserves_callback_mutations()
    {
        var fixture = Fixture(3);
        NativeBattleSiren95Frame frame = WriteRoot(fixture.Frame, task => task.WriteByte(0x29, 3));
        List<int> contacts = [];
        NativeBattleSiren95Inputs inputs = fixture.Inputs with
        {
            InitializeTarget = (current, _) => current,
            AdvanceLeaf = (current, _) => new(current, false),
            ApplyContact = (current, contact) =>
            {
                contacts.Add(contact);
                return current with { Memory = current.Memory with { Siren95 = current.Memory.Siren95!.WriteDword(0x258FCF0, (uint)contacts.Count) } };
            }
        };
        frame = NativeBattleSiren95Controller.Advance739F40(frame, inputs);
        uint director = frame.Memory.Siren95!.DirectorAddress1533010!.Value;
        frame = frame with { Memory = frame.Memory with { Siren95 = frame.Memory.Siren95.Write(director + 0x48, BitConverter.GetBytes((ushort)1)) } };
        string saved = JsonSerializer.Serialize(frame);
        frame = NativeBattleSiren95Controller.Advance739F40(frame, inputs);
        Assert.Equal(new[] { 0, 1, 2 }, contacts);
        Assert.Equal(3u, frame.Memory.Siren95!.ReadDword(0x258FCF0));
        contacts.Clear();
        NativeBattleSiren95Frame restored = NativeBattleSiren95Controller.Advance739F40(JsonSerializer.Deserialize<NativeBattleSiren95Frame>(saved)!, inputs);
        Assert.Equal(new[] { 0, 1, 2 }, contacts);
        Assert.Equal(JsonSerializer.Serialize(frame), JsonSerializer.Serialize(restored));
    }

    private static NativeBattleModulePoolTask RootTask(NativeBattleSiren95Frame frame) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(Root);

    private static NativeBattleSiren95Frame WriteRoot(NativeBattleSiren95Frame frame, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> update)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        return frame with { Effect = frame.Effect with { Pools = hierarchy.Write(Root, update(hierarchy.Read(Root))).Pools } };
    }

    internal static (NativeBattleSiren95Frame Frame, NativeBattleSiren95Inputs Inputs) Fixture(byte targets = 1, byte repeats = 1)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 2, 11, 0, 21, 1, null));
        for (int index = 0; index < Math.Max(1, (int)targets); index++)
        {
            (packets, _) = packets.AppendContact(new(new((byte)(3 + index), 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        }
        packets = packets.WritePlayerRepeatMode(0, repeats);
        (packets, _) = packets.Publish(targets);
        NativeBattleEffectParameters parameters = new(0, 1, 8, 0, 0, 0);
        NativeBattleOriginalEffectState effect = new(95, 94, new(95, 0x739DA0, 0x739D80), parameters, null)
        {
            ResourcePointers = [new(0x257F8A0, 0x10000)]
        };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), Siren95 = NativeBattleSiren95State.FromColdProcess() };
        memory = memory with { Siren95 = memory.Siren95!.WriteDword(0x258FCF0, 0x12345678) };
        NativeBattleSiren95Frame frame = NativeBattleSiren95Controller.Initialize739D80(new(memory, effect, new(1)));
        frame = NativeBattleSiren95Controller.Initialize739DA0(frame, packets);
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), new byte[] { 1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0 }.ToImmutableArray());
        ImmutableArray<bool> known = Enumerable.Repeat(true, 32).ToImmutableArray().SetItem(18, false).SetItem(19, false);
        NativeBattlePackedMatrix32 camera = new(Enumerable.Range(0, 32).Select(index => (byte)index).ToImmutableArray(), known);
        NativeBattleSiren95Inputs inputs = new(packets, camera, index => new(index, 0) { SequenceState = new() { Pose = 0, PoseFrame114 = 1 } }, _ => source, (_, _) => throw new InvalidOperationException("Target boot was not reached."), (_, _) => throw new InvalidOperationException("No leaf was admitted."), (_, _) => throw new InvalidOperationException("Contact was not reached."), (_, _) => throw new InvalidOperationException("Resource request was not reached."));
        return (frame, inputs);
    }
}
