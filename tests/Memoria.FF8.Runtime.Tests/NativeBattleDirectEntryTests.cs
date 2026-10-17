using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDirectEntryTests
{
    [Fact]
    public void CallbackDestinationsRemainIndependentAcrossLoadsAndJson()
    {
        NativeBattleEffectBinding first = new(1, 0x8D6A00, 0);
        NativeBattleEffectBinding second = new(2, 0x6298A0, 0x629880);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().PublishEntry50AF20(first, false).CopyEntry50B830().PublishEntry50AF20(second, true);
        Assert.Equal(first, memory.Entry21DFEC4);
        Assert.Equal(first, memory.Entry21DFEC8);
        Assert.Equal(second, memory.Entry21DFEC0);
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(memory))!;
        Assert.Equal(first.Entry, restored.Entry21DFEC4!.Entry);
        Assert.Equal(second.Entry, restored.Entry21DFEC0!.Entry);
        Assert.Equal(first.Entry, restored.Entry21DFEC8!.Entry);
        NativeBattleOriginalEffectState current = new(2, 1, second, null, 0);
        Assert.Throws<NotSupportedException>(() => restored.RequireDirectEntry21DFEC4(current));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectMemory.FromColdProcess().RequireDirectEntry21DFEC4(current));
    }

    [Theory]
    [InlineData(0x50BEE0u, 0xED, 0, 0, 2, 99)]
    [InlineData(0x50BEE0u, 0xEE, 0, 0, 1, 99)]
    [InlineData(0x50BDC0u, 0xF1, 0, 0, 2, 99)]
    [InlineData(0x50BDC0u, 0xF1, 5, 0, 7, 99)]
    [InlineData(0x50BDC0u, 0xF1, 6, 0, 77, 99)]
    [InlineData(0x50BDC0u, 0xF1, 65532, 0, 8, 0)]
    [InlineData(0x50BDC0u, 0xF1, 65530, 3, 8, 254)]
    public void DirectParametersPreserveSkippedOwnersAndByteWrapping(uint callback, byte command, ushort action, byte effectLowByte, byte expected80, byte expected82)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Parameter80 = 77, Parameter82 = 99 };
        NativeBattleEffectMemory result = memory.SetDirectParameters(callback, command, action, effectLowByte);
        Assert.Equal(expected80, result.Parameter80);
        Assert.Equal(expected82, result.Parameter82);
    }

    [Theory]
    [InlineData(0x50BEE0u, 0xED, 0, 1, true, 0x10)]
    [InlineData(0x50BEE0u, 0xED, 0, 2, false, 0x10)]
    [InlineData(0x50BEE0u, 0xEE, 0, 0, true, 0)]
    [InlineData(0x50BDC0u, 0xF1, 65532, 0, true, 0)]
    [InlineData(0x50BDC0u, 0xF1, 65530, 0, true, 0x10)]
    [InlineData(0x50BDC0u, 0xF1, 0, 1, true, 0x10)]
    public void DirectCompletionUsesExactReadyByteAndBranchSpecificFlagCleanup(uint callback, byte command, ushort action, byte ready, bool complete, uint flags)
    {
        var fixture = Fixture(callback, command, action);
        NativeBattleGraphicsState graphics = fixture.Graphics with { OriginalEffect = fixture.Graphics.OriginalEffect! with { Parameters = fixture.Graphics.OriginalEffect.Parameters! with { Parameter81 = ready } } };
        var result = Poll(fixture.Session, graphics, 1);
        Assert.Equal(complete, result.Complete);
        Assert.Equal(flags, result.Graphics.Flags);
        Assert.Equal(1, fixture.Session.NativeEffectMemory!.Active);
    }

    [Fact]
    public void DirectInvocationReusesTheLoadedEntryWithoutLoadingAndIgnoresItsReturnedDescriptor()
    {
        var fixture = Fixture(0x50BEE0, 0xED, 0);
        var result = Poll(fixture.Session, fixture.Graphics, 0);
        Assert.Equal((byte)1, result.Phase);
        Assert.False(result.Complete);
        Assert.Equal((uint?)0, result.Graphics.OriginalEffect!.ReturnedDescriptor);
        Assert.Equal(2, result.Graphics.OriginalEffect.Pools.Length);
        Assert.Equal((byte?)2, result.Graphics.OriginalEffect.Parameters!.Parameter80);
        Assert.Equal(1, fixture.Session.NativeEffectMemory!.Active);
        Assert.Equal(0, fixture.Session.NativeEffectMemory.Cursor);
        Assert.Null(fixture.Session.NativeEffectMemory.Entry21DFEC0);
    }

    private static (BattleSession Session, NativeBattleGraphicsState Graphics) Fixture(uint callback, byte command, ushort action)
    {
        NativeBattleEffectBinding binding = new(2, 0x6298A0, 0x629880);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().PublishEntry50AF20(binding, false) with { Active = 1 };
        BattleSession session = new(new BattleDefinition { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess() with { EffectMemory = memory }, Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.Normal);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeCamera))!.SetValue(session, NativeBattleCameraState.FromColdProcess());
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, command, 0, 0, action, 2, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        typeof(BattleSession).GetField("nativeActionPackets", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, packets);
        NativeBattleActionGraphicsState context = new(0, 0, 0, 0, 0, null, null, 1, null, callback);
        NativeBattleGraphicsState graphics = new(Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 0)).ToImmutableArray(), ["party", null, null, "enemy", null, null, null], 0x10, 0)
        {
            ActionGraphics = context,
            OriginalEffect = new(2, 1, binding, new(0, 1, 0, 0, 0, 0), 0)
        };
        return (session, graphics);
    }

    private static (NativeBattleGraphicsState Graphics, byte Phase, bool Complete) Poll(BattleSession session, NativeBattleGraphicsState graphics, byte phase)
    {
        return ((NativeBattleGraphicsState, byte, bool))typeof(BattleSession).GetMethod("PollNativeActionGraphics", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, [graphics, phase])!;
    }
}
