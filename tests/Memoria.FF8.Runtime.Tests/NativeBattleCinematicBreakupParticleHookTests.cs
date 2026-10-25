using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicBreakupParticleHookTests
{
    private const uint Basis = NativeBattleEffectNodeMemory.ArenaAddress;
    private const uint Owner = Basis + 0x2100;
    private const uint Payload = Basis + 0x4000;
    private const uint State = Basis + 0x5000;
    private const uint Bank = Basis + 0x6000;
    private const uint Program = Basis + 0x7000;

    [Fact]
    public void Zero_selector_does_not_sample_or_resolve_an_owner()
    {
        NativeBattleWaterExecutionFrame frame = PutWord(Fixture(), Payload + 0xE, 0);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicBreakupParticleHook.Emit(frame, 1, 2, 3, (_, _) => throw new InvalidOperationException());
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(Word(frame, Bank + 2), Word(result, Bank + 2));
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    public void Probability_uses_one_CRT_draw_and_includes_the_signed_threshold(short threshold, bool accepted)
    {
        NativeBattleWaterExecutionFrame frame = PutWord(Fixture(), Payload + 0x10, unchecked((ushort)threshold));
        NativeCrtRandomState expected = frame.Random.Draw(out ushort draw);
        Assert.Equal(0, draw * 256 >> 15);
        int resolutions = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicBreakupParticleHook.Emit(frame, 256, 512, 768, (current, selector) =>
        {
            resolutions++;
            Assert.Equal(expected, current.Random);
            Assert.Equal((short)0x1450, selector);
            return Owner;
        });
        Assert.Equal(expected, result.Random);
        Assert.Equal(accepted ? 1 : 0, resolutions);
        Assert.Equal(accepted ? (ushort)1 : (ushort)0, Word(result, Bank + 2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(65535)]
    public void Ring_uses_old_unsigned_cursor_and_publishes_current_owner_fields(int cursor)
    {
        NativeBattleWaterExecutionFrame frame = PutWord(Fixture(), Bank + 2, unchecked((ushort)cursor));
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicBreakupParticleHook.Emit(frame, -256, int.MinValue, 768, (_, _) => Owner);
        uint row = Bank + 0x10u + (uint)(cursor % 2 * 0x50);
        Assert.Equal(unchecked((ushort)(cursor + 1)), Word(result, Bank + 2));
        Assert.Equal((ushort)1, Word(result, Bank + 8));
        Assert.Equal((ushort)1, Word(result, row));
        Assert.Equal((ushort)0, Word(result, row + 2));
        Assert.Equal(Program, Dword(result, row + 4));
        Assert.Equal(unchecked((uint)-256), Dword(result, row + 0x14));
        Assert.Equal(0x80000000u, Dword(result, row + 0x18));
        Assert.Equal(768u, Dword(result, row + 0x1C));
        Assert.Equal(0x02BBCCDDu, Dword(result, row + 0x38));
        Assert.Equal(0u, Dword(result, row + 0x3C));
        Assert.Equal((ushort)0x1234, Word(result, row + 0x40));
        Assert.Equal((ushort)0x5678, Word(result, row + 0x48));
        Assert.Equal((byte)1, result.Arena.Read(checked((int)(row + 0x37 - Basis)), 1)[0]);
    }

    [Fact]
    public void Hook_retains_unwritten_particle_fields_and_padding()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x6010, 0x50) };
        frame = PutDword(frame, Bank + 0x40, 0xDEADBEEF);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicBreakupParticleHook.Emit(frame, 256, 512, 768, (_, _) => Owner);
        var row = result.Arena.ReadSnapshot(0x6010, 0x50);
        int[] written = Enumerable.Range(0, 8).Concat(Enumerable.Range(0x14, 0x1C)).Concat([0x37]).Concat(Enumerable.Range(0x38, 10)).Concat([0x48, 0x49]).Concat(Enumerable.Range(0x30, 4)).Distinct().Order().ToArray();
        Assert.Equal(written, Enumerable.Range(0, 0x50).Where(index => row.Known[index]));
        Assert.Equal(0xDEADBEEFu, Dword(result, Bank + 0x40));
        foreach (uint offset in new uint[] { 0x20, 0x24, 0x28, 0x2C })
        {
            Assert.Equal(0u, Dword(result, Bank + 0x10 + offset));
        }
    }

    private static NativeBattleWaterExecutionFrame Fixture()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x84, State).WriteTemporaryDword(0x90, Payload);
        frame = PutWord(frame, Payload + 0xE, 0x1450);
        frame = PutWord(frame, Payload + 0x10, 255);
        frame = PutDword(frame, State + 0x10, Program);
        frame = PutWord(frame, Bank, 2);
        frame = PutWord(frame, Bank + 2, 0);
        frame = WaterSchedulerFixture.SlotDword(frame, 1, 0xB8, Bank);
        frame = WaterSchedulerFixture.SlotDword(frame, 1, 0xCC, 0xAABBCCDD);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x92, 0x1234);
        return WaterSchedulerFixture.SlotWord(frame, 1, 0x9A, 0x5678);
    }

    private static NativeBattleWaterExecutionFrame PutWord(NativeBattleWaterExecutionFrame frame, uint address, ushort value) => frame with { Arena = frame.Arena.Write(checked((int)(address - Basis)), BitConverter.GetBytes(value)) };
    private static NativeBattleWaterExecutionFrame PutDword(NativeBattleWaterExecutionFrame frame, uint address, uint value) => frame with { Arena = frame.Arena.Write(checked((int)(address - Basis)), BitConverter.GetBytes(value)) };
    private static ushort Word(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(checked((int)(address - Basis)), 2));
    private static uint Dword(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(checked((int)(address - Basis)), 4));
}
