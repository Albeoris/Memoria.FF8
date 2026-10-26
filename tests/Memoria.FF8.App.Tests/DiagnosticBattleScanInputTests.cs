using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticBattleScanInputTests
{
    [Fact]
    public void ViewerReadyEmitsMappedCancelThenAnExplicitClear()
    {
        DiagnosticBattleScanInput input = new();
        NativeBattleOriginalEffectState effect = CreateEffect(2);

        Assert.Equal((ushort)0x40, input.Advance(effect, true, 100));
        Assert.Equal((ushort)0x40, input.Advance(effect, true, 100));
        Assert.Equal((ushort)0, input.Advance(effect, true, 101));
        Assert.Equal((ushort)0, input.Advance(effect, true, 101));
        Assert.Equal((ushort)0x40, input.Advance(effect, true, 102));
        Assert.Equal((ushort)0, input.Advance(null, false, 102));
    }

    [Fact]
    public void OpeningViewerRequiresItsOwnReadyResultChild()
    {
        DiagnosticBattleScanInput input = new();
        NativeBattleOriginalEffectState effect = CreateEffect(1);
        Assert.Equal((ushort)0, input.Advance(effect, true, 100));
        NativeBattleEffectTaskPool pool = effect.Pools[0];
        (pool, int slot) = pool.Append(0x84FAF0);
        pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteByte(0x29, 2).WriteDword(0x18, pool.BankAddress + pool.Stride)) };
        effect = effect with { Pools = [pool] };
        Assert.Equal((ushort)0, input.Advance(effect, true, 101));
        pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteDword(0x18, pool.BankAddress)) };
        Assert.Equal((ushort)0x40, input.Advance(effect with { Pools = [pool] }, true, 102));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void OtherViewerPhasesDoNotReceiveDiagnosticInput(byte phase)
    {
        Assert.Equal((ushort)0, new DiagnosticBattleScanInput().Advance(CreateEffect(phase), true, 100));
    }

    [Fact]
    public void InactiveOrUnselectedOwnersDoNotReceiveDiagnosticInput()
    {
        NativeBattleOriginalEffectState effect = CreateEffect(2);
        Assert.Equal((ushort)0, new DiagnosticBattleScanInput().Advance(effect, false, 100));
        Assert.Equal((ushort)0, new DiagnosticBattleScanInput().Advance(effect with { ReturnedDescriptor = 0 }, true, 100));
        NativeBattleEffectTaskPool pool = effect.Pools[0];
        effect = effect with { Pools = [pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0] with { Flags = 0 }) }] };
        Assert.Equal((ushort)0, new DiagnosticBattleScanInput().Advance(effect, true, 100));
    }

    private static NativeBattleOriginalEffectState CreateEffect(byte phase)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x269CFC8, 0x269CFD8, 0x78, 20);
        (pool, int slot) = pool.Append(0x84F860);
        pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteByte(0x29, phase)) };
        return new(40, 39, new(40, 0x84D030, 0x575540), null, 0x269ABA8) { Pools = [pool] };
    }
}
