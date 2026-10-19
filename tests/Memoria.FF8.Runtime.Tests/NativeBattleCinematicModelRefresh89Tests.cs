using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed partial class NativeBattleCinematicModelTests
{
    [Fact]
    public void Refresh89_clears_model_hold_for_real_advance_then_sets_it_and_uses_original_parent_translation()
    {
        NativeBattleWaterExecutionFrame frame = Refresh89Fixture();
        frame = frame with { Arena = frame.Arena.Write(Model + 2, BitConverter.GetBytes((ushort)0xA502)) };
        NativeBattleWaterExecutionFrame first = NativeBattleCinematicModelRefresh89.Execute(frame, Refresh89Matrix, Services);
        Assert.Equal((ushort)1, Word(first, Model));
        Assert.Equal((ushort)0xA502, Word(first, Model + 2));
        Assert.Equal(10u, Dword(first, Skeleton + 52));
        Assert.Equal(10u, first.ReadTemporaryDword(0xF4));
        Assert.Equal(20u, first.ReadTemporaryDword(0xF8));
        Assert.Equal(30u, first.ReadTemporaryDword(0xFC));
        NativeBattleWaterExecutionFrame second = NativeBattleCinematicModelRefresh89.Execute(first, Refresh89Matrix, Services);
        Assert.Equal((ushort)2, Word(second, Model));
        Assert.Equal((byte)2, second.Arena.Read(Model + 0x26, 1)[0]);
        Assert.Equal((ushort)0xA502, Word(second, Model + 2));
        Assert.Equal(0x1004u, WaterSchedulerFixture.ReadCoreDword(second, 0x2797450));
    }

    [Fact]
    public void Refresh89_basis_keeps_unknown_padding_and_original_temporary_translation_distinct_from_GTE_translation()
    {
        NativeBattleWaterExecutionFrame frame = Refresh89Fixture();
        frame = frame with { Core = frame.Core.MarkUnknown(0x2797968 + 18, 2) };
        var prepared = NativeBattleCinematicModelRefresh89.PrepareBasis(frame, Refresh89Matrix);
        Assert.Equal(17, prepared.Matrix.X);
        Assert.Equal(28, prepared.Matrix.Y);
        Assert.Equal(39, prepared.Matrix.Z);
        Assert.Equal(10u, prepared.Frame.ReadTemporaryDword(0xF4));
        Assert.Equal(20u, prepared.Frame.ReadTemporaryDword(0xF8));
        Assert.Equal(30u, prepared.Frame.ReadTemporaryDword(0xFC));
        Assert.False(prepared.Frame.TemporaryFrame.Known[0xF2]);
        Assert.False(prepared.Frame.TemporaryFrame.Known[0xF3]);
        Assert.Equal(0xA5A5A5A5u, prepared.Frame.ReadTemporaryDword(0x40));
    }

    [Fact]
    public void Refresh89_pause_uses_bound_control_and_snapshot_does_not_advance_or_read_animation()
    {
        NativeBattleWaterExecutionFrame frame = Refresh89Fixture().WriteTemporaryByte(0x145, 1);
        frame = frame with { Arena = frame.Arena.Write(Model, BitConverter.GetBytes((ushort)7)).Write(Model + 2, BitConverter.GetBytes((ushort)2)).MarkUnknown(Model + 0x20, 8) };
        NativeBattleCinematicModelServices services = Services with { ReadAnimations = (_, _) => throw new InvalidOperationException("Paused refresh does not consume animation data.") };
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleWaterExecutionFrame a = NativeBattleCinematicModelRefresh89.Execute(frame, Refresh89Matrix, services);
        NativeBattleWaterExecutionFrame b = NativeBattleCinematicModelRefresh89.Execute(restored, Refresh89Matrix, services);
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
        Assert.Equal((ushort)7, Word(a, Model));
        Assert.Equal((ushort)2, Word(a, Model + 2));
        Assert.Throws<NotSupportedException>(() => a.Arena.Read(Model + 0x20, 8));
    }

    private static uint Refresh89Matrix(NativeBattleWaterExecutionFrame frame, ushort selector)
    {
        Assert.Equal(0x8123, selector);
        return 0x2797968;
    }

    private static NativeBattleWaterExecutionFrame Refresh89Fixture()
    {
        NativeBattleWaterExecutionFrame frame = Prepared();
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xBC, Base + Model);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x9C, 0x8123);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8C, 256);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8E, 256);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x90, 256);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, 7);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, 8);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, 9);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973B8, frame.TemporaryFrame.Address + 0x100);
        byte[] basis = new byte[32];
        for (int index = 0; index < 3; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(basis.AsSpan(index * 8), 4096);
        }
        BinaryPrimitives.WriteInt32LittleEndian(basis.AsSpan(20), 10);
        BinaryPrimitives.WriteInt32LittleEndian(basis.AsSpan(24), 20);
        BinaryPrimitives.WriteInt32LittleEndian(basis.AsSpan(28), 30);
        return frame with { Core = frame.Core.Write(0x2797968, basis) };
    }
}
