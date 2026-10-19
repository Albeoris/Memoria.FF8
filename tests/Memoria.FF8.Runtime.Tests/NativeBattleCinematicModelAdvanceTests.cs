using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed partial class NativeBattleCinematicModelTests
{
    [Fact]
    public void Initial_draw_composes_constructor_local_matrices_without_reading_pose_or_animation()
    {
        NativeBattleWaterExecutionFrame frame = Prepared();
        frame = frame with { Arena = frame.Arena.MarkUnknown(Model + 0x20, 8).MarkUnknown(Skeleton + 8, 6).MarkUnknown(Skeleton + 20, 6).MarkUnknown(Skeleton + 68, 6) };
        NativeBattleCinematicModelServices services = Services with { ReadAnimations = (_, _) => throw new InvalidOperationException("Unexpected first draw animation read.") };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicModel.AdvanceAndCompose(frame, Base + Model, services);
        Assert.Equal((ushort)1, Word(result, Model));
        Assert.Equal(10u, Dword(result, Skeleton + 32 + 20));
        Assert.Equal(20u, Dword(result, Skeleton + 32 + 24));
        Assert.Equal(30u, Dword(result, Skeleton + 32 + 28));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(Model + 0x20, 8));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(Skeleton + 32 + 18, 2));
    }

    [Fact]
    public void Current_pose_advances_then_loops_only_on_next_exhausted_call_and_JSON_next_matches()
    {
        NativeBattleWaterExecutionFrame frame = NativeBattleCinematicModel.AdvanceAndCompose(Prepared(), Base + Model, Services);
        frame = frame with { Arena = frame.Arena.Write(Skeleton + 8, BitConverter.GetBytes((short)777)) };
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleWaterExecutionFrame second = NativeBattleCinematicModel.AdvanceAndCompose(frame, Base + Model, Services);
        Assert.Equal((ushort)2, Word(second, Model));
        Assert.Equal((byte)2, second.Arena.Read(Model + 0x26, 1)[0]);
        Assert.Equal((short)777, WordSigned(second, Skeleton + 8));
        Assert.Equal(2174u, Dword(second, Skeleton + 52));
        Assert.Equal(JsonSerializer.Serialize(second), JsonSerializer.Serialize(NativeBattleCinematicModel.AdvanceAndCompose(restored, Base + Model, Services)));
        NativeBattleWaterExecutionFrame third = NativeBattleCinematicModel.AdvanceAndCompose(second, Base + Model, Services);
        Assert.Equal((ushort)3, Word(third, Model));
        Assert.Equal((byte)1, third.Arena.Read(Model + 0x26, 1)[0]);
        Assert.Equal((short)0, WordSigned(third, Skeleton + 8));
        Assert.Equal(10u, Dword(third, Skeleton + 52));
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 0)]
    public void Pause_and_hold_refresh_current_raw_pose_without_advancing_sequence_or_counter(bool paused, ushort flags)
    {
        NativeBattleWaterExecutionFrame frame = Prepared();
        frame = frame with { Arena = frame.Arena.Write(Model, BitConverter.GetBytes((ushort)7)).Write(Model + 2, BitConverter.GetBytes(flags)).Write(Skeleton + 8, BitConverter.GetBytes((short)50)).MarkUnknown(Model + 0x20, 8) };
        frame = frame.WriteTemporaryByte(0x145, paused ? (byte)1 : (byte)0);
        frame = frame with { Core = frame.Core.Write(NativeBattleScriptedEffectMemory.Address + 0x45, new byte[] { paused ? (byte)0 : (byte)1 }) };
        if (paused)
        {
            frame = frame with { Arena = frame.Arena.MarkUnknown(Model + 2, 2) };
        }
        NativeBattleCinematicModelServices services = Services with { ReadAnimations = (_, _) => throw new InvalidOperationException("Unexpected held animation read.") };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicModel.AdvanceAndCompose(frame, Base + Model, services);
        Assert.Equal((ushort)7, Word(result, Model));
        Assert.Equal(149u, Dword(result, Skeleton + 52));
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(Model + 0x20, 8));
    }

    [Fact]
    public void Counter_wrap_normalizes_to_one_without_reading_unneeded_loop_selector()
    {
        NativeBattleWaterExecutionFrame frame = Prepared();
        frame = frame with { Arena = frame.Arena.Write(Model, BitConverter.GetBytes(ushort.MaxValue)).MarkUnknown(Model + 9, 1) };
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicModel.AdvanceAndCompose(frame, Base + Model, Services);
        Assert.Equal((ushort)1, Word(result, Model));
        Assert.Equal((byte)2, result.Arena.Read(Model + 0x26, 1)[0]);
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(Model + 9, 1));
    }

    private static NativeBattleWaterExecutionFrame Prepared()
    {
        NativeBattleWaterExecutionFrame frame = NativeBattleCinematicModel.Allocate(Fixture(), 21, Services).Frame;
        byte[] matrix = new byte[32];
        for (int index = 0; index < 3; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(matrix.AsSpan(index * 8), 4096);
        }
        BinaryPrimitives.WriteInt32LittleEndian(matrix.AsSpan(20), 10);
        BinaryPrimitives.WriteInt32LittleEndian(matrix.AsSpan(24), 20);
        BinaryPrimitives.WriteInt32LittleEndian(matrix.AsSpan(28), 30);
        return frame.WriteTemporary(0xE0, matrix).WriteTemporaryByte(0x145, 0);
    }
}
