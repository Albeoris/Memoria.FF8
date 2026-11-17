using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGroup31PublicationTests
{
    [Theory]
    [InlineData((ushort)265)]
    [InlineData((ushort)266)]
    [InlineData((ushort)267)]
    [InlineData((ushort)268)]
    [InlineData((ushort)269)]
    [InlineData((ushort)270)]
    [InlineData((ushort)274)]
    [InlineData((ushort)276)]
    [InlineData((ushort)279)]
    [InlineData((ushort)282)]
    public void Finished_selected_owner_clears_only_transient_prior_ledger_before_zero_pointer_poll(ushort effectId)
    {
        NativeBattlePrimitiveReference reusedAddress = new("off_B6D070", 0, 52);
        NativeBattleOriginalEffectState selected = new(effectId, effectId - 1, new(effectId, 1, 0), null, 0) { PresentationPackets = [reusedAddress] };
        string frozen = JsonSerializer.Serialize(selected);
        NativeBattleOriginalEffectState coldSelected = JsonSerializer.Deserialize<NativeBattleOriginalEffectState>(frozen)!;
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { ReturnedTaskPointer1D96AAC = 0, DetachedReturnedTask = null };
        string frozenMemory = NativeBattleTestSnapshots.Json(memory);
        NativeBattleGraphicsState graphics = new(new BattleActorModels([]), NativeBattleModelBoundary.FromRetainedMemory(null), 28, 329, Memoria.FF8.Tests.NativeBattleTypedFixture.OriginalEffects()) { OriginalEffect = selected, PrimitiveFrame = NativeBattlePrimitiveFrame.Clear() };
        BattlePresentationProcess session = (BattlePresentationProcess)RuntimeHelpers.GetUninitializedObject(typeof(BattlePresentationProcess));
        typeof(BattlePresentationProcess).GetField("nativeEffectMemory", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(session, memory);
        MethodInfo poll = typeof(BattlePresentationProcess).GetMethod("AdvanceNativeOriginalEffect", BindingFlags.NonPublic | BindingFlags.Instance)!;
        NativeBattleGraphicsState direct = (NativeBattleGraphicsState)poll.Invoke(session, [graphics])!;
        NativeBattleGraphicsState cold = (NativeBattleGraphicsState)poll.Invoke(session, [graphics with { OriginalEffect = coldSelected }])!;
        Assert.Same(memory, session.Memory);
        Assert.Equal(frozenMemory, NativeBattleTestSnapshots.Json(session.Memory));
        Assert.Equal(NativeBattleTestSnapshots.Json(graphics), NativeBattleTestSnapshots.Json(direct));
        Assert.Equal(NativeBattleTestSnapshots.Json(direct), NativeBattleTestSnapshots.Json(cold));
        Assert.Equal(frozen, JsonSerializer.Serialize(direct.OriginalEffect));
        Assert.Contains(reusedAddress, selected.PresentationPackets);
        Assert.Empty(direct.OriginalEffect!.PresentationPackets);
        Assert.Equal(cold.OriginalEffect!.PresentationPackets, direct.OriginalEffect.PresentationPackets);
    }
}
