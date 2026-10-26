using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect218Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect218CleanupTests
{
    [Fact]
    public void Common_reload_starts_at_old_age_one_and_waits_for_the_real_completion_word()
    {
        (NativeBattleEffect218Frame frame, uint address) = Fixture(0, 0);
        List<NativeBattleEffect218RestoreCommon508630> requests = [];
        NativeBattleEffect218Frame Restore(NativeBattleEffect218Frame current, NativeBattleEffect218RestoreCommon508630 request)
        {
            requests.Add(request);
            return current;
        }
        var first = AdvanceCleanup633BF0(frame, address, Restore);
        Assert.Empty(requests);
        Assert.Equal(0, first.Result);
        var second = AdvanceCleanup633BF0(first.Frame, address, Restore);
        Assert.Equal(new NativeBattleEffect218RestoreCommon508630(ArenaAddress + StreamedIoOffset, address + 14), Assert.Single(requests));
        Assert.Equal(0, second.Result);
        var waiting = AdvanceCleanup633BF0(second.Frame, address, Restore);
        Assert.Single(requests);
        Assert.Equal(0, waiting.Result);
        NativeBattleEffect218Frame completed = Write(waiting.Frame, address, Read(waiting.Frame, address).WriteByte(14, 1));
        var final = AdvanceCleanup633BF0(completed, address, Restore);
        Assert.Equal(2, final.Result);
        Assert.Equal((ushort)4, Read(final.Frame, address).ReadWord(12));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(256)]
    public void Cleanup_reads_the_full_native_completion_word_and_wraps_its_age(ushort completion)
    {
        (NativeBattleEffect218Frame frame, uint address) = Fixture(ushort.MaxValue, completion);
        var result = AdvanceCleanup633BF0(frame, address, (_, _) => throw new InvalidOperationException());
        Assert.Equal(2, result.Result);
        Assert.Equal((ushort)0, Read(result.Frame, address).ReadWord(12));
        Assert.Equal(completion, Read(result.Frame, address).ReadWord(14));
    }

    private static (NativeBattleEffect218Frame Frame, uint Address) Fixture(ushort age, ushort completion)
    {
        NativeBattleCureFamilyPool profile = Pools[1];
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(profile.Descriptor, profile.Bank, profile.Stride, profile.Capacity);
        (pool, int slot) = pool.Append(0x633BF0);
        pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(12, age).WriteWord(14, completion)) };
        NativeBattleOriginalEffectState effect = new(218, 218, new(218, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor) { Pools = ImmutableArray.Create(pool) };
        NativeBattleEffect218State state = NativeBattleEffect218State.FromColdProcess().WriteDword(ArenaGlobal, ArenaAddress);
        return (new(NativeBattleEffectMemory.FromColdProcess(), effect, new(1), state), pool.BankAddress + (uint)(slot * pool.Stride));
    }
}
