using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanControlInputTests
{
    [Fact]
    public void TypedScanInputRequiresAnExplicitSupportedButtonAndOnePulse()
    {
        new BattleInput(BattleInputKind.ScanInput) { ScanButton = BattleScanButton.Confirm }.Validate();
        Assert.Throws<InvalidDataException>(() => new BattleInput(BattleInputKind.ScanInput).Validate());
        Assert.Throws<InvalidDataException>(() => new BattleInput(BattleInputKind.Tick) { ScanButton = BattleScanButton.Confirm }.Validate());
        Assert.Throws<InvalidDataException>(() => new BattleInput(BattleInputKind.ScanInput, Frames: 2) { ScanButton = BattleScanButton.Confirm }.Validate());
        Assert.Throws<InvalidDataException>(() => new BattleInput(BattleInputKind.ScanInput) { ScanButton = (BattleScanButton)99 }.Validate());
    }

    [Fact]
    public void BattleWithoutANativeViewerCannotAcceptScanInput()
    {
        BattleSession battle = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.Normal);
        Assert.False(battle.CanQueueNativeScanInput);
        Assert.Throws<InvalidOperationException>(() => battle.QueueNativeScanInput(BattleScanButton.Confirm));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void ReadyViewerInputIsIndependentOfTheScannedActorsFaction(byte target)
    {
        NativeBattleOriginalEffectState effect = Effect(2, target);
        Assert.True(NativeBattleScanInputControl.IsReady(effect, ReadyProcess()));
        Assert.False(NativeBattleScanInputControl.IsReady(effect with { ReturnedDescriptor = 0 }, ReadyProcess()));
        Assert.False(NativeBattleScanInputControl.IsReady(effect, ReadyProcess() with { InputInitialized = false }));
        Assert.False(NativeBattleScanInputControl.IsReady(effect, ReadyProcess() with { Text = NativeBattleScanTextProcess.FromColdProcess() }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void ConfirmRequiresTheActualViewerConfirmationPhase(byte phase)
    {
        Assert.False(NativeBattleScanInputControl.IsReady(Effect(phase, 0), ReadyProcess()));
    }

    [Fact]
    public void ViewerResultPageHasItsOwnNativeConfirmationBoundary()
    {
        NativeBattleOriginalEffectState effect = Effect(1, 0);
        NativeBattleEffectTaskPool pool = effect.Pools[0];
        (pool, int result) = pool.Append(0x84FAF0);
        pool = pool.WriteSlot(result, pool.Slots[result].WriteByte(0x29, 2).WriteDword(0x18, pool.BankAddress));
        Assert.True(NativeBattleScanInputControl.IsReady(effect with { Pools = [pool] }, ReadyProcess()));
        pool = pool.WriteSlot(result, pool.Slots[result].WriteDword(0x18, pool.BankAddress + pool.Stride));
        Assert.False(NativeBattleScanInputControl.IsReady(effect with { Pools = [pool] }, ReadyProcess()));
    }

    [Fact]
    public void PulseSurvivesSnapshotAndPublishesOneDownUpEdgeThroughCapturedButtonMapping()
    {
        byte[] map = Enumerable.Range(1, 12).Select(value => (byte)value).ToArray();
        map[4] = 7;
        map[6] = 5;
        const uint options = 0x20;
        NativeBattleScanProcess process = ReadyProcess() with { ControlPulse = new(BattleScanButton.Confirm) };
        process = JsonSerializer.Deserialize<NativeBattleScanProcess>(JsonSerializer.Serialize(process))!;
        NativeBattleScanInputPublicationState state = NativeBattleScanInputPublication.Initialize49F540(process.Input, _ => 0);
        state = NativeBattleScanInputConfiguration.Configure4B9460(state, process.Mouse1D2B2E0).State;
        NativeBattleScanControlSample press = NativeBattleScanInputControl.Sample(process.ControlPulse!, 100);
        ushort raw = NativeInputConfiguration.InvertPulse(press.MappedButtons, options, map);
        Assert.Equal((ushort)0x10, raw);
        state = NativeBattleScanInputPublication.Publish49E9C0(state, _ => raw);
        Assert.Equal((ushort)0x40, NativeInputConfiguration.Project(state.Input.Read49ED30(0, 0), options, map));
        Assert.Equal((ushort)0x10, state.Input.Read49EDE0(0, 0));
        NativeBattleScanControlPulse resumed = JsonSerializer.Deserialize<NativeBattleScanControlPulse>(JsonSerializer.Serialize(press.Pending))!;
        NativeBattleScanControlSample sameCallback = NativeBattleScanInputControl.Sample(resumed, 100);
        state = NativeBattleScanInputPublication.Publish49E9C0(state, _ => NativeInputConfiguration.InvertPulse(sameCallback.MappedButtons, options, map));
        Assert.Equal((ushort)0x40, NativeInputConfiguration.Project(state.Input.Read49ED30(0, 0), options, map));
        Assert.Equal((ushort)0, state.Input.Read49EDE0(0, 0));
        NativeBattleScanControlSample release = NativeBattleScanInputControl.Sample(resumed, 101);
        Assert.Null(release.Pending);
        Assert.Equal((ushort)0, release.MappedButtons);
        state = NativeBattleScanInputPublication.Publish49E9C0(state, _ => 0);
        Assert.Equal((ushort)0, state.Input.Read49ED30(0, 0));
        Assert.Throws<InvalidDataException>(() => NativeBattleScanInputControl.Sample(resumed, 99));
    }

    private static NativeBattleScanProcess ReadyProcess()
    {
        NativeBattleScanProcess cold = NativeBattleScanProcess.FromColdProcess();
        return cold with { InputInitialized = true, Text = cold.Text with { Initialized = true } };
    }

    private static NativeBattleOriginalEffectState Effect(byte phase, byte target)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x269CFC8, 0x269CFD8, 0x78, 20);
        (pool, int slot) = pool.Append(0x84F860);
        pool = pool.WriteSlot(slot, pool.Slots[slot].WriteByte(0x29, phase).WriteByte(0x2D, target));
        return new(40, 39, new(40, 0x84D030, 0x575540), null, 0x269ABA8) { Pools = [pool] };
    }
}
