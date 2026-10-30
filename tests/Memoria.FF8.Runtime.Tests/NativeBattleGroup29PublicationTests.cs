using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGroup29PublicationTests
{
    [Theory]
    [InlineData((ushort)219)]
    [InlineData((ushort)227)]
    [InlineData((ushort)228)]
    [InlineData((ushort)236)]
    [InlineData((ushort)237)]
    [InlineData((ushort)243)]
    [InlineData((ushort)244)]
    [InlineData((ushort)245)]
    [InlineData((ushort)246)]
    [InlineData((ushort)247)]
    public void Finished_selected_owner_clears_only_transient_prior_ledger_before_zero_pointer_poll(ushort effectId)
    {
        NativeBattlePrimitiveReference reusedAddress = new("off_B6D070", 0, 52);
        NativeBattleOriginalEffectState selected = new(effectId, effectId - 1, new(effectId, 1, 0), null, 0) { PresentationPackets = [reusedAddress] };
        string frozen = JsonSerializer.Serialize(selected);
        NativeBattleOriginalEffectState coldSelected = JsonSerializer.Deserialize<NativeBattleOriginalEffectState>(frozen)!;
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { ReturnedTaskPointer1D96AAC = 0, DetachedReturnedTask = null };
        string frozenMemory = NativeBattleTestSnapshots.Json(memory);
        NativeBattleGraphicsState graphics = new([], [], 28, 329) { OriginalEffect = selected, PrimitiveFrame = NativeBattlePrimitiveFrame.Clear() };
        BattleSession session = (BattleSession)RuntimeHelpers.GetUninitializedObject(typeof(BattleSession));
        typeof(BattleSession).GetField("nativeEffectMemory", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(session, memory);
        MethodInfo poll = typeof(BattleSession).GetMethod("AdvanceNativeOriginalEffect", BindingFlags.NonPublic | BindingFlags.Instance)!;
        NativeBattleGraphicsState direct = (NativeBattleGraphicsState)poll.Invoke(session, [graphics])!;
        NativeBattleGraphicsState cold = (NativeBattleGraphicsState)poll.Invoke(session, [graphics with { OriginalEffect = coldSelected }])!;
        Assert.Same(memory, session.NativeEffectMemory);
        Assert.Equal(frozenMemory, NativeBattleTestSnapshots.Json(session.NativeEffectMemory));
        Assert.Equal(NativeBattleTestSnapshots.Json(graphics), NativeBattleTestSnapshots.Json(direct));
        Assert.Equal(NativeBattleTestSnapshots.Json(direct), NativeBattleTestSnapshots.Json(cold));
        Assert.Equal(frozen, JsonSerializer.Serialize(direct.OriginalEffect));
        Assert.Contains(reusedAddress, selected.PresentationPackets);
        Assert.Empty(direct.OriginalEffect!.PresentationPackets);
        Assert.Equal(cold.OriginalEffect!.PresentationPackets, direct.OriginalEffect.PresentationPackets);
    }
}
