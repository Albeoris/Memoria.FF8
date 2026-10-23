using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect83Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect83Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect83ControllerTests
{
    [Theory]
    [InlineData(0, 0x8000, 0x10000)]
    [InlineData(1, 0, 0x8000)]
    [InlineData(256, 0x8000, 0x10000)]
    public void Root_uses_task_low_parity_and_publishes_canonical_current_view(int parity, int first, int second)
    {
        Harness harness = new();
        NativeBattleEffect83Frame frame = Active();
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(0x5C, (ushort)parity));
        frame = Advance(frame, null!, harness.Inputs());
        Assert.Equal(0x20DFAB8u + (uint)first, frame.State.ReadDword(0x25C9F74));
        Assert.Equal(0x20DFAB8u + (uint)second, frame.State.ReadDword(0x25CA07C));
        Assert.Equal(ViewAddress, frame.State.ReadDword(0x25CFA40));
        Assert.Equal(1234, frame.Memory.SharedEffectView2793E58!.ReadMatrix().Z);
        Assert.Equal((ushort)(parity + 1), Read(frame, RootBank).ReadWord(0x5C));
        Assert.Equal((ushort)1, Read(frame, RootBank).ReadWord(0x24));
    }

    [Fact]
    public void Root_clears_exact_addressed_auxiliary_bytes_and_waits_for_actual_texture_completion()
    {
        Harness harness = new();
        NativeBattleEffect83Frame frame = Active();
        uint first = frame.State.ReadDword(0x25CFA4C);
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(0x10000 - 1, [0xA5]).Write(0x10000 + 0x11760, [0x5A]) } };
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(0x29, 2));
        frame = Advance(frame, null!, harness.Inputs());
        Assert.Equal(first, frame.State.ReadDword(0x25CFA2C));
        Assert.Equal(first + 0x10E0, frame.State.ReadDword(0x25CFA44));
        Assert.Equal(first + 0x11760, frame.State.ReadDword(0x25CFA4C));
        Assert.Equal(new byte[0x11760], frame.Memory.Arena!.Read(0x10000, 0x11760));
        Assert.Equal((byte)0xA5, frame.Memory.Arena.Read(0xFFFF, 1)[0]);
        Assert.Equal((byte)0x5A, frame.Memory.Arena.Read(0x21760, 1)[0]);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(0x29, 7));
        frame = Advance(frame, null!, harness.Inputs());
        Assert.Equal(new NativeBattleEffect83TextureLoad508630(0x20DFAB8, RootBank + 0x62), Assert.Single(harness.Operations.OfType<NativeBattleEffect83TextureLoad508630>()));
        frame = Advance(frame, null!, harness.Inputs());
        Assert.Equal((byte)8, Read(frame, RootBank).ReadByte(0x29));
        Assert.Equal(RootDescriptor, frame.Effect.ReturnedDescriptor);
        frame = WriteCompletionByte(frame, RootBank + 0x62, 1);
        frame = Advance(frame, null!, harness.Inputs());
        frame = Advance(frame, null!, harness.Inputs());
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Empty(frame.Effect.Pools);
        Assert.All(frame.State.BankStorage, bank => Assert.NotNull(bank));
        string json = JsonSerializer.Serialize(frame);
        NativeBattleEffect83Frame restored = JsonSerializer.Deserialize<NativeBattleEffect83Frame>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(Advance(restored, null!, harness.Inputs())));
    }

    [Fact]
    public void Driver_request_and_acknowledgement_remain_separate_callback_states()
    {
        Harness harness = new();
        (NativeBattleEffect83Frame frame, uint driver) = Child(2, 0x797BF0, 0x48, harness);
        frame = Write(frame, driver, Read(frame, driver).WriteByte(0x29, 1));
        frame = AdvanceDriver(frame, driver, null!, harness.Inputs()).Frame;
        Assert.Equal((ushort)0, frame.State.ReadWord(TargetAddress + 0x40));
        Assert.Equal((ushort)0, frame.State.ReadWord(TargetAddress + 0x42));
        Assert.Equal((ushort)1, frame.State.ReadWord(TargetAddress + 0x44));
        frame = AdvanceDriver(frame, driver, null!, harness.Inputs()).Frame;
        Assert.Equal((ushort)0, frame.State.ReadWord(TargetAddress + 0x40));
        Assert.Equal((ushort)1, frame.State.ReadWord(TargetAddress + 0x42));
        Assert.Equal((byte)2, Read(frame, driver).ReadByte(0x29));
        Assert.Empty(harness.Operations.OfType<NativeBattleEffect83Sound501330>());
    }

    [Fact]
    public void Main_contact_uses_actual_signed_contact_selector_after_handshake()
    {
        Harness harness = new();
        (NativeBattleEffect83Frame frame, uint main) = Child(1, 0x797A90, 0x58, harness);
        frame = Write(frame, main, Read(frame, main).WriteByte(0x29, 1).WriteByte(0x2B, 1));
        frame = AdvanceMain(frame, main, null!, harness.Inputs()).Frame;
        Assert.Empty(harness.Operations.OfType<NativeBattleEffect83Contact506690>());
        frame = frame with { State = frame.State.WriteWord(TargetAddress + 0x48, 1) };
        frame = AdvanceMain(frame, main, null!, harness.Inputs()).Frame;
        Assert.Equal(new NativeBattleEffect83Contact506690(1), Assert.Single(harness.Operations.OfType<NativeBattleEffect83Contact506690>()));
        Assert.Equal((byte)2, Read(frame, main).ReadByte(0x29));
    }

    [Fact]
    public void Four_transition_rows_follow_exact_ramp_clamps_and_retirement()
    {
        Harness harness = new();
        (NativeBattleEffect83Frame frame, uint address) = Child(4, 0x79D860, 0xB0, harness);
        frame = AdvanceStageTransition(frame, address, harness.Inputs()).Frame;
        Assert.Equal(4, harness.Operations.OfType<NativeBattleEffect83StageTransition>().Count(item => item.ClearRgb));
        for (int index = 1; index <= 4; index++)
        {
            frame = AdvanceStageTransition(frame, address, harness.Inputs()).Frame;
            Assert.Equal((ushort)(index * 512), Read(frame, address).ReadWord(0x1C));
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(0x24, 80));
        frame = AdvanceStageTransition(frame, address, harness.Inputs()).Frame;
        NativeBattleEffect83Step step = new(frame, 0);
        for (int index = 0; index < 8; index++)
        {
            step = AdvanceStageTransition(step.Frame, address, harness.Inputs());
            Assert.Equal(index == 7 ? 2 : 0, step.Result);
        }
        Assert.Equal((ushort)0, Read(step.Frame, address).ReadWord(0x1C));
        Assert.Equal((byte)0, Read(step.Frame, RootBank).ReadByte(0x28));
        Assert.Equal(52, harness.Operations.OfType<NativeBattleEffect83StageTransition>().Count());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Target_capture_uses_current_physical_row_and_exact_byte_then_word_flag_writes(int target)
    {
        Harness harness = new();
        (NativeBattleEffect83Frame frame, uint address) = Child(5, 0x79D9B0, 0xC8, harness);
        frame = Write(frame, address, Read(frame, address).WriteByte(0x2D, (byte)target).WriteWord(0x24, 54));
        frame = AdvanceTargetBody(frame, address, harness.Inputs()).Frame;
        Assert.Empty(harness.Operations.OfType<NativeBattleEffect83TargetCapture79E590>());
        frame = AdvanceTargetBody(frame, address, harness.Inputs()).Frame;
        Assert.Single(harness.Operations.OfType<NativeBattleEffect83TargetCapture79E590>());
        NativeBattleEffect83Step step = new(frame, 0);
        for (int index = 0; index < 90; index++)
        {
            step = AdvanceTargetBody(step.Frame, address, harness.Inputs());
        }
        Assert.Equal(2, step.Result);
        Assert.Equal(new NativeBattleEffect83TargetFlags[] { new(PhysicalModelAddress(target), 0xFF, 4, false), new(PhysicalModelAddress(target), 0xFF, 0x20, false), new(PhysicalModelAddress(target), 0xFFFB, 0, true) }, harness.Operations.OfType<NativeBattleEffect83TargetFlags>());
        Assert.Equal(90, harness.Operations.OfType<NativeBattleEffect83TargetDraw79DA20>().Count());
        Assert.Equal((ushort)0, Read(step.Frame, address).ReadWord(0xC0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2048)]
    [InlineData(4096)]
    [InlineData(-1024)]
    public void Owned_model_material_fades_use_current_two_colors_signed_division_and_retained_code(short factor)
    {
        Harness harness = new();
        (NativeBattleEffect83Frame frame, uint address) = Child(7, 0x79E690, 0x140, harness);
        frame = Write(frame, address, Read(frame, address).WriteWord(0x13A, unchecked((ushort)factor)).WriteWord(0x13C, unchecked((ushort)factor)).WriteDword(0x5C, 0xA6000000));
        frame = UpdateModelColors(frame, address, harness.Inputs());
        NativeBattleModulePoolTask task = Read(frame, address);
        for (int channel = 0; channel < 3; channel++)
        {
            int transition = (byte)(0x12345678u >> (channel * 8));
            int normal = (byte)(0x90ABCDEFu >> (channel * 8));
            Assert.Equal(unchecked((byte)(transition - transition * factor / 4096)), task.ReadByte(0x58 + channel));
            Assert.Equal(unchecked((byte)(normal - normal * factor / 4096)), task.ReadByte(0x5C + channel));
        }
        Assert.Equal((byte)0xA6, task.ReadByte(0x5F));
        Assert.Equal(factor == 0 ? (byte)0 : (byte)2, task.ReadByte(0x5B));
        Assert.Equal(factor == 0 ? (byte)0 : (byte)(unchecked((byte)(255 - 255 * factor / 4096)) >> 2), task.ReadByte(0x37));
    }

    [Fact]
    public void Addressed_context_aliases_existing_shared_scratch_and_preserves_unknown_padding()
    {
        NativeBattleEffect83Frame frame = Active();
        NativeBattleEffectArena context = Context(frame, 0x2793DF0, 104).MarkUnknown(18, 2).Write(64, [1, 2, 3, 4]);
        frame = PublishContext(frame, 0x2793DF0, context, 104);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, frame.Memory.NodeScratch2793E30!.Bytes.AsSpan().Slice(0, 4).ToArray());
        Assert.Throws<NotSupportedException>(() => ReadBytes(frame, 0x2793E02, 2).Read(0, 2));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, ReadBytes(frame, 0x2793E30, 4).Read(0, 4));
    }

    private static (NativeBattleEffect83Frame Frame, uint Address) Child(int pool, uint callback, int length, Harness harness) => AppendHierarchy(Active(), pool, callback, length, RootBank, harness.Inputs());

    private static NativeBattleEffect83Frame Active()
    {
        NativeBattleOriginalEffectState effect = new(83, 82, new(83, Entry, Initializer), new(4, 0, 0x7F, 0, 0, 0), null);
        NativeBattleEffect83State state = NativeBattleEffect83State.FromColdProcess().WriteDword(InitializerGlobal, 0x20DFAB8).WriteDword(0x25CF3EC, 0x20DFAB8).WriteDword(0x25CF3E8, 0x20DFAB8 + 0x8000).WriteDword(0x25CED2C, 0x20DFAB8 + 0x8000).WriteDword(0x25CED28, 0x20DFAB8 + 0x10000).WriteDword(0x25CFA4C, 0x20DFAB8 + 0x10000);
        ImmutableArray<NativeBattleEffectTaskPool> pools = Pools.Select(shape => NativeBattleEffectTaskPool.Initialize(shape.Descriptor, shape.Bank, shape.Stride, shape.Capacity)).ToImmutableArray();
        NativeBattleEffectHierarchyAppend root = new NativeBattleEffectHierarchy(pools).Append8DC540(0, RootCallback, 100);
        for (int index = 0; index < Pools.Length; index++)
        {
            state = state.DetachBank(index);
        }
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() };
        NativeBattleEffect83Frame frame = new(memory, effect with { Pools = root.Hierarchy.Pools, ReturnedDescriptor = RootDescriptor }, NativeCrtRandomState.FromColdProcess(), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        return Write(frame, RootBank, NativeBattleEffectRootTask.Initialize(Read(frame, RootBank), effect.Parameters!, Packets()));
    }

    private static NativeBattleActionPacketsState Packets()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[NativeBattleActionPacketsState.HeaderCapacity * 20], new byte[NativeBattleActionPacketsState.ContactCapacity * 24]);
        (packets, _) = packets.Prepare(new(4, 8, 16, -1, 336, 83, null));
        (packets, _) = packets.AppendContact(new(new(6, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.AppendContact(new(new(0, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(2);
        return packets;
    }

    private sealed class Harness
    {
        public List<NativeBattleEffect83Operation> Operations { get; } = [];

        public NativeBattleEffect83Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, frame => Packets(), frame => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 1234 }, null), frame => new(null, 100, 160, 108), (frame, index) => new(index, 2), (frame, index) => throw new NotSupportedException("This scoped phase does not read a pose source."), frame => 0, (frame, source) => source == 0xB8B7D8 ? 0x12345678u : source == 0xB8B9A8 ? 0x90ABCDEFu : throw new InvalidDataException(), (frame, operation) =>
        {
            Operations.Add(operation);
            return frame;
        });
    }
}
