using static Memoria.FF8.Runtime.NativeBattleEffect218Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect218BurstTests
{
    [Theory]
    [InlineData(0x634750u, 13, -25, -19)]
    [InlineData(0x634890u, 21, -26, -18)]
    [InlineData(0x634A10u, 15, -25, -17)]
    public void Burst_draws_before_pause_and_retirement_with_signed_velocity_decay_and_WORD_wrapping(uint callback, ushort age, short horizontal, short vertical)
    {
        NativeBattleCureFamilyPool profile = Pools[1];
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(profile.Descriptor, profile.Bank, profile.Stride, profile.Capacity);
        (pool, int slot) = pool.Append(callback);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(12, age).WriteWord(14, 0xAA55).WriteWord(24, 0x8002).WriteWord(26, unchecked((ushort)-8)).WriteWord(28, 0x8002).WriteWord(30, unchecked((ushort)-31)).WriteWord(32, 0x8002).WriteWord(34, unchecked((ushort)-19));
        pool = pool.WriteSlot(slot, task);
        NativeBattleOriginalEffectState effect = new(218, 218, new(218, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor) { Pools = [pool] };
        NativeBattleEffect218Frame frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(1), NativeBattleEffect218State.FromColdProcess());
        uint address = pool.BankAddress + (uint)(slot * pool.Stride);
        List<ushort> ages = [];
        NativeBattleEffect218Frame Draw(NativeBattleEffect218Frame current, uint selected)
        {
            ages.Add(Read(current, selected).ReadWord(12));
            return current;
        }
        var paused = AdvanceBurst218(frame, address, 1, Draw);
        Assert.Equal(0, paused.Result);
        Assert.Equal((ushort)0x8002, Read(paused.Frame, address).ReadWord(28));
        var actual = AdvanceBurst218(paused.Frame, address, 0, Draw);
        Assert.Equal(new ushort[] { age, age }, ages);
        Assert.Equal(2, actual.Result);
        task = Read(actual.Frame, address);
        Assert.Equal((ushort)(age + 1), task.ReadWord(12));
        Assert.Equal((ushort)0x7FE3, task.ReadWord(28));
        Assert.Equal(horizontal, Signed(task, 30));
        Assert.Equal(vertical, Signed(task, 34));
        Assert.Equal((ushort)0xAA55, task.ReadWord(14));
        Assert.Equal(callback == 0x634750 ? (ushort)0x8002 : (ushort)0x7FFA, task.ReadWord(24));
        Assert.Equal(callback == 0x634750 ? (ushort)0x8002 : (ushort)0x7FEF, task.ReadWord(32));
    }
}
