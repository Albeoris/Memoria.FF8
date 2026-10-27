using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect316Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect316Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect316ControllerTests
{
    private const uint Caster = 0x1D97494;

    [Fact]
    public void Entry_queues_both_TIMs_transfers_root_and_retains_warm_native_random()
    {
        NativeBattleOriginalEffectState effect = new(316, 315, new(316, Entry, Initializer), new(3, 0, 1, 0, 0, 0), null)
        {
            ResourcePointers = [new(InitializerGlobal, 0x40000)]
        };
        NativeBattleEffect316State state = NativeBattleEffect316State.FromColdProcess().WriteDword(RandomGlobal, 0x5678).WriteDword(ColorGlobal, 0xA1B2C3D4);
        NativeBattleEffect316Frame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, new(19), state);
        List<NativeBattleEffect316Operation> operations = [];
        NativeBattleEffect316Frame result = Initialize(frame, Resources(), Inputs(operations));
        Assert.Equal(new uint[] { CommonAddress + 0x40000, EmbeddedTimAddress }, operations.OfType<NativeBattleEffect316UploadTim505E30>().Select(operation => operation.SourceAddress));
        Assert.Equal(0x5678u, result.State.ReadDword(RandomGlobal));
        Assert.Equal(0xA1B2C3D4u, result.State.ReadDword(ColorGlobal));
        Assert.Null(result.State.FixedBanks[RootBank]);
        Assert.Throws<InvalidDataException>(() => result.State.ReadBank(RootBank));
        Assert.Equal(new byte[8], TaskView(Read(result, RootBank), 12, 8).Read(0, 8));
        Assert.Equal(Caster, result.State.ReadDword(CasterGlobal));
        Assert.Equal(CommonAddress, result.State.ReadDword(WorkGlobal));
        NativeBattleEffect316ModelFlags caster = Assert.IsType<NativeBattleEffect316ModelFlags>(operations[2]);
        Assert.Equal(2, caster.Width);
        Assert.Equal(0xFFF3, caster.AndMask);
        Assert.Equal(3, operations.OfType<NativeBattleEffect316ModelFlags>().Count(operation => operation.OrMask == 4));
        Assert.IsType<NativeBattleEffect316RegisterCamera5099A0>(operations[^1]);
        Assert.Equal(frame.Random, result.Random);
    }

    [Fact]
    public void Root_starts_main_at_two_and_preserves_current_color_and_exact_bank_ownership()
    {
        NativeBattleEffect316Frame frame = RootFrame();
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 2));
        List<NativeBattleEffect316Operation> operations = [];
        NativeBattleEffect316Step result = AdvanceRoot661190(frame, RootBank, Resources(), Inputs(operations));
        NativeBattleModulePoolTask main = Read(result.Frame, CommonAddress);
        Assert.Equal(MainCallback, main.Callback);
        Assert.Equal(1, main.ReadWord(12));
        Assert.Equal(0x12345678u, main.ReadDword(0x10));
        Assert.Equal(0xAB123456u, main.ReadDword(0x14));
        Assert.Equal(CommonAddress + RecordOffset, main.ReadDword(RecordHintOffset));
        Assert.Equal(new byte[0x800], TaskView(main, RecordOffset, 0x800).Read(0, 0x800));
        Assert.Equal((byte)main.Flags, result.Frame.Memory.Arena!.Read(0, 1)[0]);
        Assert.Equal(1, Read(result.Frame, RootBank).ReadByte(15));
        Assert.Equal(3, Read(result.Frame, RootBank).ReadWord(12));
        Assert.Equal(CommonAddress + SecondPacketOffset, result.Frame.State.ReadDword(CursorGlobal));
        Assert.IsType<NativeBattleEffect316Music47E3C0>(operations[0]);
        Assert.IsType<NativeBattleEffect316Vibration4A29A0>(operations[1]);
        Assert.IsType<NativeBattleEffect316ClearGeometryMarkers6612C0>(operations[2]);
        Assert.IsType<NativeBattleEffect316AdvanceRecords662720>(operations[3]);
        Assert.Equal(0, Assert.IsType<NativeBattleEffect316Stage5713E0>(operations[4]).Amount);
    }

    [Fact]
    public void Root_double_buffer_starts_do_not_create_a_private_bank_capacity_or_reset_rng()
    {
        NativeBattleEffect316Frame frame = RootFrame();
        frame = frame with { State = frame.State.WriteDword(RandomGlobal, 0x6ABC).WriteDword(AmplitudeGlobal, 123) };
        frame = AdvanceRoot661190(frame, RootBank, Resources(), Inputs()).Frame;
        Assert.Equal(CommonAddress + SecondPacketOffset, frame.State.ReadDword(CursorGlobal));
        Assert.Equal(0u, frame.State.ReadDword(AmplitudeGlobal));
        frame = AdvanceRoot661190(frame, RootBank, Resources(), Inputs()).Frame;
        Assert.Equal(CommonAddress + FirstPacketOffset, frame.State.ReadDword(CursorGlobal));
        Assert.Equal(0x6ABCu, frame.State.ReadDword(RandomGlobal));
        NativeBattleEffectArena bytes = NativeBattleEffectArena.Clear().Write(0, [9, 8, 7, 6]);
        frame = WriteBytes(frame, CommonAddress + FirstPacketOffset + 0x10000, bytes, 4);
        Assert.Equal(new byte[] { 9, 8, 7, 6 }, ReadBytes(frame, CommonAddress + SecondPacketOffset, 4).Read(0, 4));
    }

    [Theory]
    [InlineData(19, 0, 0, false)]
    [InlineData(20, 1, 512, false)]
    [InlineData(119, 1, -3448, false)]
    [InlineData(120, 1, -3488, false)]
    [InlineData(149, 1, -3488, false)]
    [InlineData(150, 2, -3488, true)]
    [InlineData(289, 2, 219, true)]
    [InlineData(290, 2, 246, false)]
    [InlineData(299, 2, 486, false)]
    [InlineData(300, 0, 0, false)]
    public void Main_phase_boundaries_pass_exact_coordinates_and_color_widths(short age, int lane, int scale, bool blend)
    {
        NativeBattleEffect316Frame frame = MainFrame(age);
        List<NativeBattleEffect316Operation> operations = [];
        NativeBattleEffect316Step result = AdvanceMain661350(frame, CommonAddress, Resources(), Inputs(operations));
        Assert.Equal(0, result.Result);
        Assert.Equal(unchecked((ushort)(age + 1)), Read(result.Frame, CommonAddress).ReadWord(12));
        Assert.Equal(lane == 1 ? 1 : 0, operations.OfType<NativeBattleEffect316Draw661550>().Count());
        Assert.Equal(lane == 2 ? 1 : 0, operations.OfType<NativeBattleEffect316Draw661CC0>().Count());
        if (lane == 1)
        {
            NativeBattleEffect316Draw661550 draw = Assert.Single(operations.OfType<NativeBattleEffect316Draw661550>());
            Assert.Equal((Caster, scale, 11, 0x414), (draw.ModelAddress, draw.X, draw.Y, draw.Z));
        }
        if (lane == 2)
        {
            NativeBattleEffect316Draw661CC0 draw = Assert.Single(operations.OfType<NativeBattleEffect316Draw661CC0>());
            Assert.Equal((CommonAddress, Caster, CommonAddress + ProjectedRowsOffset, scale, 11, 0x414), (draw.MainAddress, draw.ModelAddress, draw.ProjectedRowsAddress, draw.X, draw.Y, draw.Z));
        }
        Assert.Equal(blend ? 1 : 0, operations.OfType<NativeBattleEffect316Blend56CC00>().Count());
        if (blend)
        {
            NativeBattleEffect316Blend56CC00 operation = Assert.Single(operations.OfType<NativeBattleEffect316Blend56CC00>());
            Assert.Equal(CommonAddress + 0x14, operation.Source1Address);
            Assert.Equal(ColorGlobal, operation.Source2Address);
            Assert.Equal(Caster + 0x2C, operation.DestinationAddress);
            Assert.Equal(4096, operation.Weight1 + operation.Weight2);
            NativeBattleEffect316WriteModel alpha = Assert.Single(operations.OfType<NativeBattleEffect316WriteModel>());
            Assert.Equal((0x2F, 1), (alpha.Offset, alpha.Length));
            Assert.Equal(new byte[] { 0x32 }, alpha.Data.Read(0, 1));
        }
        Assert.All(operations.OfType<NativeBattleEffect316ModelFlags>(), operation => Assert.Equal(1, operation.Width));
        Assert.Single(operations.OfType<NativeBattleEffect316AdvanceRecords662720>());
    }

    [Theory]
    [InlineData(-1, -125)]
    [InlineData(0, 0)]
    [InlineData(7, 875)]
    [InlineData(8, 1000)]
    [InlineData(304, 1000)]
    [InlineData(305, 875)]
    [InlineData(311, 125)]
    public void Signed_main_amplitude_and_clock_follow_native_word_arithmetic(short age, int amplitude)
    {
        NativeBattleEffect316Frame frame = MainFrame(age);
        NativeBattleEffect316Step result = AdvanceMain661350(frame, CommonAddress, Resources(), Inputs());
        Assert.Equal(unchecked((uint)amplitude), result.Frame.State.ReadDword(AmplitudeGlobal));
        Assert.Equal(unchecked((ushort)(age + 1)), Read(result.Frame, CommonAddress).ReadWord(12));
        Assert.Equal(frame.Random, result.Frame.Random);
        Assert.Equal(frame.State.ReadDword(RandomGlobal), result.Frame.State.ReadDword(RandomGlobal));
    }

    [Fact]
    public void Finite_native_color_division_matches_all_reached_interpolation_weights()
    {
        for (uint phase = 0; phase < 140; phase++)
        {
            Assert.Equal((int)(phase * 4096 / 140), ColorFraction(phase));
        }
    }

    [Fact]
    public void Retirement_restores_raw_Color44_and_word_flags_without_draw_or_age_increment()
    {
        NativeBattleEffect316Frame frame = MainFrame(312);
        frame = Write(frame, CommonAddress, Read(frame, CommonAddress).WriteDword(0x14, 0xFE123456));
        List<NativeBattleEffect316Operation> operations = [];
        NativeBattleEffect316Step result = AdvanceMain661350(frame, CommonAddress, Resources(), Inputs(operations));
        Assert.Equal(2, result.Result);
        Assert.Equal(312, Read(result.Frame, CommonAddress).ReadWord(12));
        Assert.Equal(5, operations.Count);
        Assert.Equal((Caster, 2, (ushort)0xFFF7), (Assert.IsType<NativeBattleEffect316ModelFlags>(operations[0]).ModelAddress, ((NativeBattleEffect316ModelFlags)operations[0]).Width, ((NativeBattleEffect316ModelFlags)operations[0]).AndMask));
        NativeBattleEffect316WriteModel restore = Assert.IsType<NativeBattleEffect316WriteModel>(operations[1]);
        Assert.Equal((Caster, 0x2C, 4), (restore.ModelAddress, restore.Offset, restore.Length));
        Assert.Equal(0xFE123456u, BinaryPrimitives.ReadUInt32LittleEndian(restore.Data.Read(0, 4)));
        Assert.All(operations.Skip(2).Cast<NativeBattleEffect316ModelFlags>(), operation =>
        {
            Assert.Equal(2, operation.Width);
            Assert.Equal(0xFFFB, operation.AndMask);
        });
    }

    [Theory]
    [InlineData(45, true, false)]
    [InlineData(46, false, false)]
    [InlineData(307, false, false)]
    [InlineData(308, false, true)]
    public void Sound_and_vibration_stop_use_exact_ages_and_actual_handle(short age, bool sound, bool stop)
    {
        NativeBattleEffect316Frame frame = MainFrame(age);
        frame = Write(frame, CommonAddress, Read(frame, CommonAddress).WriteDword(0x10, 0x87654321));
        List<NativeBattleEffect316Operation> operations = [];
        AdvanceMain661350(frame, CommonAddress, Resources(), Inputs(operations));
        Assert.Equal(sound ? 1 : 0, operations.OfType<NativeBattleEffect316Sound501330>().Count());
        Assert.Equal(stop ? 1 : 0, operations.OfType<NativeBattleEffect316StopVibration4A2940>().Count());
        if (sound)
        {
            Assert.Equal(new NativeBattleEffect316Sound501330(SoundAddress, 0, 128), Assert.Single(operations.OfType<NativeBattleEffect316Sound501330>()));
        }
        if (stop)
        {
            Assert.Equal(unchecked((int)0x87654321), Assert.Single(operations.OfType<NativeBattleEffect316StopVibration4A2940>()).Handle);
        }
    }

    [Fact]
    public void Root_retirement_returns_current_COMMON_payload_and_exclusive_warm_snapshot()
    {
        NativeBattleEffect316Frame frame = MainFrame(312);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 42).WriteByte(15, 1));
        frame = frame with { State = frame.State.WriteDword(RandomGlobal, 0x7BEE).WriteDword(ColorGlobal, 0xABCD1234) };
        frame = WriteBytes(frame, CommonAddress + 0x300, NativeBattleEffectArena.Clear().Write(0, [4, 5, 6]), 3);
        NativeBattleEffect316Frame result = Advance(frame, Resources(), Inputs());
        Assert.Empty(result.Effect.Pools);
        Assert.Equal(0u, result.Effect.ReturnedDescriptor);
        Assert.Equal(42, BinaryPrimitives.ReadUInt16LittleEndian(result.State.ReadBank(RootBank).Read(12, 2)));
        Assert.Equal(new byte[] { 4, 5, 6 }, result.Memory.Arena!.Read(0x300, 3));
        result = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect316Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)))!);
        result.State.Validate();
        Assert.Equal(0x7BEEu, result.State.ReadDword(RandomGlobal));
        Assert.Equal(0xABCD1234u, result.State.ReadDword(ColorGlobal));
        Assert.NotNull(result.State.ReadBank(RootBank));
    }

    [Fact]
    public void Addressed_copy_crosses_active_main_boundary_without_reviving_stale_COMMON()
    {
        NativeBattleEffect316Frame frame = MainFrame(0);
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, [1, 2, 3, 4, 5, 6, 7, 8]).MarkUnknown(6, 1);
        frame = WriteBytes(frame, CommonAddress + RecordHintOffset, source, 8);
        NativeBattleEffectArena actual = ReadBytes(frame, CommonAddress + RecordHintOffset, 8);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, actual.Read(0, 6));
        Assert.Throws<NotSupportedException>(() => actual.Read(6, 1));
        Assert.Equal(8, actual.Read(7, 1)[0]);
        Assert.Equal(0x04030201u, Read(frame, CommonAddress).ReadDword(RecordHintOffset));
        Assert.Equal((byte)1, frame.Memory.Arena!.Read(RecordHintOffset, 1)[0]);
    }

    private static NativeBattleEffect316Frame RootFrame()
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, 1);
        (root, _) = root.Append(RootCallback);
        NativeBattleOriginalEffectState effect = new(316, 315, new(316, Entry, Initializer), null, RootDescriptor) { Pools = [root] };
        NativeBattleEffect316State state = NativeBattleEffect316State.FromColdProcess().DetachBank(RootBank).WriteDword(WorkGlobal, CommonAddress).WriteDword(CasterGlobal, Caster).WriteDword(RandomGlobal, 0x1234);
        return new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, new(123), state);
    }

    private static NativeBattleEffect316Frame MainFrame(short age)
    {
        NativeBattleEffect316Frame frame = InitializeMainBank(RootFrame());
        (frame, _) = Append(frame, MainDescriptor, MainCallback);
        frame = ClearDwords6DA380(frame, CommonAddress + 12, 0x204);
        return Write(frame, CommonAddress, Read(frame, CommonAddress).WriteWord(12, unchecked((ushort)age)));
    }

    private static NativeBattleEffect316Inputs Inputs(List<NativeBattleEffect316Operation>? operations = null) => new(
        Colors: NativeBattleGteColorInterpolation.CodeConstants45F270,
        ReadPackets: null!,
        ReadModel: (_, _) => new(3, 0xFFFF) { Color44 = 0xAB123456 },
        ReadView: null!,
        ReadProjection: null!,
        ReadCameraRoll: null!,
        ReadArena: null!,
        ReadGeometryAddress: null!,
        ResolveBoneRecord: null!,
        AllocateTemporary: null!,
        ReleaseTemporary: null!,
        Execute: (frame, operation) =>
        {
            operations?.Add(operation);
            if (operation is NativeBattleEffect316Vibration4A29A0 vibration)
            {
                return WriteBytes(frame, vibration.OutputAddress, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(0x12345678u)), 4);
            }
            return frame;
        });

    private static NativeBattleEffect316Resources Resources()
    {
        byte[] camera = new byte[256];
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(0), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(2), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(4), 24);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(6), 256);
        byte[] vibration = new byte[270];
        BinaryPrimitives.WriteUInt32LittleEndian(vibration.AsSpan(4), 256);
        BinaryPrimitives.WriteUInt16LittleEndian(vibration.AsSpan(256), 6);
        BinaryPrimitives.WriteUInt16LittleEndian(vibration.AsSpan(258), 4);
        byte[] sprites = new byte[364];
        BinaryPrimitives.WriteUInt16LittleEndian(sprites.AsSpan(8), 12);
        for (int index = 1; index <= 11; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(sprites.AsSpan(8 + 2 * index), 40);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(sprites.AsSpan(34), ushort.MaxValue);
        return new(new("mag315.tim", Tim(131)), Tim(128), new("camera", camera.ToImmutableArray()), new("vibration", vibration.ToImmutableArray()), 0, new(0, ProjectedQuadsAddress, 0, 0, sprites.ToImmutableArray()));
    }

    private static ImmutableArray<byte> Tim(int imageHeight)
    {
        int imageLength = 12 + 256 * imageHeight * 2;
        byte[] bytes = new byte[8 + 524 + imageLength];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0), 16);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 9);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 524);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 256);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(18), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(532), (uint)imageLength);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(540), 256);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(542), (ushort)imageHeight);
        return bytes.ToImmutableArray();
    }
}
