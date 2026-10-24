using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect318Controller;
using static Memoria.FF8.Runtime.Tests.NativeBattleEffect318LowerTests;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect318RecordTests
{
    [Fact]
    public void Full_bank_returns_before_CRT_or_current_GTE_consumption()
    {
        Fixture fixture = new(0x65E790);
        for (int index = 0; index < 128; index++)
        {
            fixture.Task(task => task.WriteWord(0xB06 + index * 16, 1));
        }
        NativeBattleEffect318Frame result = NativeBattleEffect318Records.Birth65F710(fixture.Frame, fixture.Address, null!, null!, null!, fixture.Inputs());
        Assert.Same(fixture.Frame, result);
    }

    [Fact]
    public void Free_hint_precedes_earlier_free_row_and_success_consumes_one_CRT()
    {
        Fixture fixture = new(0x65E790);
        uint address = fixture.Address + 0xB00 + 5 * 16;
        fixture.Task(task => task.WriteDword(0x1300, address));
        fixture.Frame = WriteBytes(fixture.Frame, address, ReadBytes(fixture.Frame, address, 16).MarkUnknown(10, 2).MarkUnknown(15, 1), 16);
        fixture.Frame = fixture.Frame with { Memory = fixture.Frame.Memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(fixture.Frame.Memory.GeometryRegisters!, NativeBattleCuragaMatrices.Identity8DD770() with { Z = 999 }, true, true, null) } };
        NativeBattleEffectArena point = NativeBattleEffectArena.Clear().Write(4, BitConverter.GetBytes((short)3000)).MarkUnknown(6, 2);
        NativeBattleEffectArena direction = NativeBattleEffectArena.Clear().MarkUnknown(2, 2).MarkUnknown(6, 2);
        NativeCrtRandomState expected = fixture.Frame.Random.Draw(out ushort random);
        NativeBattleEffect318Frame result = NativeBattleEffect318Records.Birth65F710(fixture.Frame, fixture.Address, point, direction, fixture.Resources, fixture.Inputs());
        NativeBattleEffectArena row = ReadBytes(result, address, 16);
        Assert.Equal(11, NativeBattleEffect318Lower.Word318(row, 6));
        Assert.Equal((random >> 4) + 2048, NativeBattleEffect318Lower.Word318(row, 8));
        Assert.Equal(expected, result.Random);
        Assert.Equal(16, row.Read(14, 1)[0]);
        Assert.Equal(0, NativeBattleEffect318Lower.Word318(ReadBytes(result, fixture.Address + 0xB06, 2), 0));
        Assert.Equal(address + 16, Read(result, fixture.Address).ReadDword(0x1300));
        Assert.DoesNotContain(true, row.ReadSnapshot(10, 2).Known);
        Assert.DoesNotContain(true, row.ReadSnapshot(15, 1).Known);
    }

    [Fact]
    public void Recordscan_draws_before_decrement_and_recaptures_current_temporary_W()
    {
        Fixture fixture = new(0x65E790);
        uint first = fixture.Address + 0xB00;
        for (int index = 0; index < 2; index++)
        {
            NativeBattleEffectArena row = NativeBattleEffectArena.Clear().Write(4, BitConverter.GetBytes((short)(2000 + index * 200))).Write(6, BitConverter.GetBytes((short)(2 - index))).Write(8, BitConverter.GetBytes((short)4096)).Write(12, [0xFF]);
            fixture.Frame = WriteBytes(fixture.Frame, first + (uint)(index * 16), row, 16);
        }
        fixture.Frame = fixture.Frame with { Staging = fixture.Frame.Staging!.MarkUnknown(NativeBattleTemporaryMemory.Address + 0x62, 2).MarkUnknown(NativeBattleTemporaryMemory.Address + 0x76, 2).MarkUnknown(NativeBattleTemporaryMemory.Address + 0x96, 2) };
        int delivered = 0;
        NativeBattleEffect318Inputs inputs = fixture.Inputs() with { Execute = (frame, operation) =>
        {
            delivered++;
            Assert.Equal(40, Assert.IsType<NativeBattleEffect318DrawBatch>(operation).Batch.AllocationBytes);
            if (delivered == 2)
            {
                Assert.Equal((short)0x55AA, BinaryPrimitives.ReadInt16LittleEndian(frame.Memory.ScriptedColorRegisters!.NormalInput!.Value.AsSpan().Slice(6)));
            }
            return frame with { Staging = frame.Staging!.Write(NativeBattleTemporaryMemory.Address + 0x62, BitConverter.GetBytes((short)0x55AA)) };
        } };
        NativeBattleEffect318Frame result = NativeBattleEffect318Records.Advance65F340(fixture.Frame, fixture.Address, fixture.Resources, inputs);
        Assert.Equal(2, delivered);
        Assert.Equal(1, NativeBattleEffect318Lower.Word318(ReadBytes(result, first, 16), 6));
        Assert.Equal(0, NativeBattleEffect318Lower.Word318(ReadBytes(result, first + 16, 16), 6));
        Assert.Equal((short)-4, NativeBattleEffect318Lower.Word318(ReadBytes(result, first, 16), 0));
        Assert.Equal(fixture.Frame.Random, result.Random);
        Assert.DoesNotContain(true, NativeBattleTemporaryMemory.ReadView(result.Staging!, NativeBattleTemporaryMemory.Address, 180).ReadSnapshot(0x96, 2).Known);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Empty_scan_reads_only_lifetime_and_still_publishes_exact_initial_WORD_zeros()
    {
        Fixture fixture = new(0x65E790);
        for (int index = 0; index < 128; index++)
        {
            fixture.Frame = WriteBytes(fixture.Frame, fixture.Address + 0xB00 + (uint)(index * 16), NativeBattleEffectArena.Clear().MarkUnknown(0, 16).Write(6, [0, 0]), 16);
        }
        fixture.Frame = fixture.Frame with { Staging = fixture.Frame.Staging!.MarkUnknown(NativeBattleTemporaryMemory.Address, 180) };
        NativeBattleEffect318Frame result = NativeBattleEffect318Records.Advance65F340(fixture.Frame, fixture.Address, null!, fixture.Inputs());
        Assert.Equal(fixture.Frame.Memory.GeometryRegisters, result.Memory.GeometryRegisters);
        Assert.Empty(fixture.Operations);
        NativeBattleEffectArena temp = NativeBattleTemporaryMemory.ReadView(result.Staging!, NativeBattleTemporaryMemory.Address, 180);
        Assert.Equal(new byte[2], temp.Read(0x48, 2));
        Assert.Equal(new byte[2], temp.Read(0x86, 2));
        Assert.DoesNotContain(true, temp.ReadSnapshot(0x84, 2).Known);
        Assert.DoesNotContain(true, temp.ReadSnapshot(0x96, 2).Known);
    }
}
