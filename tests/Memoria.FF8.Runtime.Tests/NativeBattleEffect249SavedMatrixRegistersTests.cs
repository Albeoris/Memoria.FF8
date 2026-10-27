using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect249SavedMatrixRegistersTests
{
    private const uint Saved = 0x2798B68;
    private const uint FirstRow = 0x2797A08;
    private static readonly NativeBattleWaterSlotBank Bank = new(0x2000, 0x400, 4, 0x2000);

    [Theory]
    [InlineData(0x104)]
    [InlineData(0x105)]
    public void Transform_publishes_source_basis_and_actual_input_without_reloading_product(int opcode)
    {
        NativeBattleActionMatrix basis = QuarterTurn(100, -200, 300);
        NativeBattleWaterExecutionFrame frame = Fixture((ushort)opcode, basis);
        Registers owner = new();
        NativeBattleGeometryRegisterStep before = owner.Current;
        NativeBattleWaterExecutionFrame result = owner.Execute(frame);
        Assert.Equal(1, owner.Publications);
        Assert.Equal(new int?[] { 80, -190, 270 }, owner.Current.Colors.Mac.ToArray());
        Assert.Equal(new int?[] { 80, -190, 270 }, owner.Current.Colors.Ir.ToArray());
        Assert.Equal(0u, owner.Current.Colors.Flags);
        Assert.Equal(new int?[] { 100, -200, 300 }, owner.Current.Geometry.Translation.ToArray());
        Assert.Equal(Bytes(basis)[..20].Select(value => (byte?)value), owner.Current.Geometry.Rotation20);
        Assert.Equal(new byte[] { 10, 0, 20, 0, 226, 255, 0x34, 0x12 }, owner.Current.Colors.NormalInput!.Value.ToArray());
        Assert.Null(owner.Current.Colors.NormalInputKnown);
        uint destination = opcode == 0x104 ? Saved : FirstRow;
        Assert.Equal(new short[] { -4096, 0, 0, 0, -4096, 0, 0, 0, 4096 }, Rotation(result, destination));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(result.Core.Read(destination + 18, 2)));
        Assert.Equal(new[] { 80, -190, 270 }, Translation(result, destination));
        Assert.Equal(opcode == 0x104 ? 0u : 0x1000u, result.ReadTemporaryDword(0xF0));
        AssertRetained(before, owner.Current);
        Assert.Equal(frame.ModuleViewBackup, result.ModuleViewBackup);
        Assert.Equal(frame.Random, result.Random);
    }

    [Theory]
    [InlineData(0x104)]
    [InlineData(0x105)]
    public void Full_MAC_and_clamped_IR_flags_are_published_independently_of_destination_rotation(int opcode)
    {
        NativeBattleActionMatrix basis = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 40000, -50000, 60000, 4096);
        Registers owner = new();
        NativeBattleWaterExecutionFrame result = owner.Execute(Fixture((ushort)opcode, basis));
        Assert.Equal(new int?[] { 40010, -49980, 59970 }, owner.Current.Colors.Mac.ToArray());
        Assert.Equal(new int?[] { 32767, -32768, 32767 }, owner.Current.Colors.Ir.ToArray());
        Assert.Equal(0x81C00000u, owner.Current.Colors.Flags);
        Assert.Equal(opcode == 0x104 ? 0x81C00000u : 0x81C01000u, result.ReadTemporaryDword(0xF0));
        Assert.Equal(new[] { 40010, -49980, 59970 }, Translation(result, opcode == 0x104 ? Saved : FirstRow));
        Assert.Equal(new int?[] { 40000, -50000, 60000 }, owner.Current.Geometry.Translation.ToArray());
    }

    [Fact]
    public void Aliased_load_captures_saved_translation_before_destination_stores()
    {
        NativeBattleActionMatrix basis = QuarterTurn(100, 200, 300);
        NativeBattleWaterExecutionFrame frame = Fixture(0x105, basis, -139);
        Assert.Equal(FirstRow, unchecked(Saved + (uint)(32 * -139)));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x12, 99);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797206, 99);
        Registers owner = new();
        NativeBattleWaterExecutionFrame result = owner.Execute(frame);
        Assert.Equal(new[] { 80, 210, 270 }, Translation(result, FirstRow));
        Assert.Equal(new int?[] { 100, 200, 300 }, owner.Current.Geometry.Translation.ToArray());
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(owner.Current.Geometry.Rotation20.Take(2).Select(value => value!.Value).ToArray()));
        Assert.Equal((short)-4096, Rotation(result, FirstRow)[0]);
        Assert.Equal(new int?[] { 80, 210, 270 }, owner.Current.Colors.Mac.ToArray());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Saved_padding_knownness_is_loaded_per_byte_while_destination_padding_is_explicit_zero(bool lowKnown, bool highKnown)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x105, QuarterTurn(100, -200, 300));
        if (!lowKnown)
        {
            frame = frame with { Core = frame.Core.MarkUnknown(Saved + 18, 1) };
        }
        if (!highKnown)
        {
            frame = frame with { Core = frame.Core.MarkUnknown(Saved + 19, 1) };
        }
        Registers owner = new();
        NativeBattleWaterExecutionFrame result = owner.Execute(frame);
        Assert.Equal(lowKnown ? (byte?)0xA5 : null, owner.Current.Geometry.Rotation20[18]);
        Assert.Equal(highKnown ? (byte?)0x5A : null, owner.Current.Geometry.Rotation20[19]);
        (byte[] padding, bool[] known) = result.Core.ReadSnapshot(FirstRow + 18, 2);
        Assert.Equal(new byte[] { 0, 0 }, padding);
        Assert.Equal(new[] { true, true }, known);
        Assert.Equal(new int?[] { 80, -190, 270 }, owner.Current.Colors.Mac.ToArray());
    }

    [Theory]
    [InlineData(0x104, 6)]
    [InlineData(0x105, 7)]
    public void Unconsumed_V0_W_byte_remains_unknown_without_blocking_XYZ_math(int opcode, int unknownLane)
    {
        NativeBattleWaterExecutionFrame frame = Fixture((ushort)opcode, QuarterTurn(100, -200, 300));
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x2094 + unknownLane, 1) };
        Registers owner = new();
        NativeBattleWaterExecutionFrame result = owner.Execute(frame);
        Assert.False(owner.Current.Colors.NormalInputKnown!.Value[unknownLane]);
        Assert.All(owner.Current.Colors.NormalInputKnown.Value.Take(6), value => Assert.True(value));
        Assert.Equal(new int?[] { 80, -190, 270 }, owner.Current.Colors.Mac.ToArray());
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x2094 + unknownLane, 1));
    }

    [Fact]
    public void JSON_next_preserves_full_frame_registers_and_partial_knownness()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x105, QuarterTurn(100, -200, 300));
        frame = frame with { Core = frame.Core.MarkUnknown(Saved + 19, 1), Arena = frame.Arena.MarkUnknown(0x209B, 1) };
        Registers first = new();
        frame = first.Execute(frame);
        SavedOwners owners = new(frame, first.Current);
        string json = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(owners));
        SavedOwners restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<SavedOwners>(json)!);
        Assert.Equal(json, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)));
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        NativeBattleWaterExecutionFrame restoredFrame = WaterSchedulerFixture.CoreDword(restored.Frame, 0x2797450, 0x1000);
        Registers direct = new(first.Current);
        Registers cold = new(restored.Registers);
        NativeBattleWaterExecutionFrame expected = direct.Execute(frame);
        NativeBattleWaterExecutionFrame actual = cold.Execute(restoredFrame);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(new SavedOwners(expected, direct.Current))), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(new SavedOwners(actual, cold.Current))));
    }

    [Theory]
    [InlineData(0x104)]
    [InlineData(0x105)]
    public void Saved_commands_require_register_services_and_roll_back_the_caller_transaction(int opcode)
    {
        NativeBattleWaterExecutionFrame frame = Fixture((ushort)opcode, QuarterTurn(100, -200, 300));
        string before = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame));
        using (NativeBattleEffectArenaTransaction transaction = new(frame.Core.Core))
        using (NativeBattleWaterTemporaryFrame.PrivateWrite temporary = frame.TemporaryFrame.BeginPrivateWrite())
        {
            Assert.Throws<NotSupportedException>(() => NativeBattleWaterMatrixCommands.Execute(frame with { Core = new(transaction.View), TemporaryFrame = temporary.View }, Program(), Bank));
        }
        Assert.Equal(before, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unknown_consumed_matrix_or_vector_byte_cannot_publish_partial_registers(bool matrixUnknown)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x105, QuarterTurn(100, -200, 300));
        frame = matrixUnknown ? frame with { Core = frame.Core.MarkUnknown(Saved, 1) } : frame with { Arena = frame.Arena.MarkUnknown(0x2094, 1) };
        Registers owner = new();
        string frameBefore = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame));
        string registersBefore = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(owner.Current));
        Assert.Throws<NotSupportedException>(() => owner.Execute(frame));
        Assert.Equal(0, owner.Publications);
        Assert.Equal(registersBefore, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(owner.Current)));
        Assert.Equal(frameBefore, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)));
    }

    [Fact]
    public void Unrelated_uniform_color_command_does_not_require_register_services()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x80D4, QuarterTurn(0, 0, 0));
        frame = frame with { Arena = frame.Arena.Write(0x1002, new byte[] { 7, 0 }) };
        NativeBattleWaterExecutionFrame result = NativeBattleWaterMatrixCommands.Execute(frame, Program(), Bank);
        Assert.Equal(0x00070707u, WaterSchedulerFixture.ReadSlotDword(result, 0, 0xCC));
    }

    private static void AssertRetained(NativeBattleGeometryRegisterStep before, NativeBattleGeometryRegisterStep after)
    {
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(before.Geometry with { Rotation20 = after.Geometry.Rotation20, Translation = after.Geometry.Translation })), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(after.Geometry)));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(before.Colors with { Mac = after.Colors.Mac, Ir = after.Colors.Ir, Flags = after.Colors.Flags, NormalInput = after.Colors.NormalInput, NormalInputKnown = after.Colors.NormalInputKnown })), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(after.Colors)));
    }

    private static NativeBattleActionMatrix QuarterTurn(int x, int y, int z) => new([0, -4096, 0, 4096, 0, 0, 0, 0, 4096], x, y, z, 4096);
    private static NativeBattleWaterScriptProgram Program() => new(0x1000, 0x100, 0x1000);

    private static NativeBattleWaterExecutionFrame Fixture(ushort opcode, NativeBattleActionMatrix basis, short index = 0)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, opcode);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        frame = frame with { Arena = frame.Arena.Write(0x1000, new byte[] { (byte)opcode, (byte)(opcode >> 8), (byte)index, (byte)(index >> 8), 0xFF, 0xFF }) };
        frame = frame with { ModuleViewBackup = Bytes(basis).ToImmutableArray(), Core = frame.Core.Write(unchecked(Saved + (uint)(32 * index)), Bytes(basis)) };
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, 10);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, 20);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, unchecked((ushort)-30));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x9A, 0x1234);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8C, 0);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8E, 0);
        return WaterSchedulerFixture.SlotWord(frame, 0, 0x90, 1024);
    }

    private static byte[] Bytes(NativeBattleActionMatrix matrix)
    {
        byte[] bytes = new byte[32];
        for (int lane = 0; lane < 9; lane++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(lane * 2), matrix.Rotation[lane]);
        }
        bytes[18] = 0xA5;
        bytes[19] = 0x5A;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), matrix.X);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), matrix.Y);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28), matrix.Z);
        return bytes;
    }

    private static short[] Rotation(NativeBattleWaterExecutionFrame frame, uint address)
    {
        byte[] bytes = frame.Core.Read(address, 18);
        return Enumerable.Range(0, 9).Select(lane => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(lane * 2))).ToArray();
    }

    private static int[] Translation(NativeBattleWaterExecutionFrame frame, uint address)
    {
        byte[] bytes = frame.Core.Read(address + 20, 12);
        return Enumerable.Range(0, 3).Select(lane => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(lane * 4))).ToArray();
    }

    private sealed record SavedOwners(NativeBattleWaterExecutionFrame Frame, NativeBattleGeometryRegisterStep Registers);

    private sealed class Registers
    {
        public NativeBattleGeometryRegisterStep Current { get; private set; }
        public int Publications { get; private set; }

        public Registers(NativeBattleGeometryRegisterStep? initial = null)
        {
            Current = initial ?? new(NativeBattleGeometryRegisters.Unknown() with { V1 = [1, null, 3, 4, 5, 6, 7, 8], V2 = [8, 7, 6, 5, 4, 3, 2, null], Sxy = [11, null, 33], Sz = [44, 55, null, 77], Mac0 = 99, Otz = 88, Lzcs = 123, Lzcr = 25, AnchorScratch8 = [9, 8, 7, null, 5, 4, 3, 2] }, new([101, 102, 103], [201, 202, 203], [301, null, 303], 0xFFFFFFFF) { Ir0 = 777, Material = 0xAABBCCDD });
        }

        public NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame)
        {
            NativeBattleWaterActorAnchorServices services = new(_ => Current, (current, registers) =>
            {
                Current = registers;
                Publications++;
                return current;
            });
            return NativeBattleWaterMatrixCommands.Execute(frame, Program(), Bank, services);
        }
    }
}
