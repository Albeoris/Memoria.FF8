using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChoco98StateTests
{
    [Fact]
    public void Cold98_state_has_its_proved_scratch_and_unknown_external_span_without_warming97()
    {
        NativeBattleChoco98State state = NativeBattleChoco98State.FromColdProcess();
        state.Validate();
        Assert.Equal(new byte[0x168], state.MorphScratch);
        Assert.Null(state.ProjectedScratch);
        Assert.Null(state.Io);
        Assert.Equal(0x13184, state.PendingIoBanks!.Length);
        Assert.Throws<NotSupportedException>(() => state.PendingIoBanks.Read(0x19DB018, 1));
        NativeBattleChoco97State other = NativeBattleChoco97State.FromColdProcess();
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Choco97 = other, Choco98 = state };
        NativeBattleChocoControllerState common = NativeBattleChocoControllerState.Read(memory, true);
        NativeBattleEffectMemory changed = (common with { FreezePrimary = 1, FreezeSecondary = 2 }).Write(memory, true);
        Assert.Same(other, changed.Choco97);
        Assert.Equal((uint)1, changed.Choco98!.FreezePrimary);
        Assert.Equal((uint)2, changed.Choco98.FreezeSecondary);
        Assert.Null(other.FreezePrimary);
    }

    [Fact]
    public void Neutral_controller_view_retains_actual98_IO_schema_and_one_bank_owner_across_JSON()
    {
        NativeBattleChoco98State cold = NativeBattleChoco98State.FromColdProcess();
        NativeBattleChoco98IoState io = NativeBattleChoco98Io.Initialize721CF0(new byte[0x120].ToImmutableArray(), 0x20DFAB8 + 0x20000, cold.PendingIoBanks!).State;
        NativeBattleChoco98State initialized = cold with { Context = NativeBattleChoco98Context.Clear722150(), Io = io, PendingIoBanks = null, IoScriptKnownCold = false };
        initialized.Validate();
        NativeBattleChoco97State prior97 = NativeBattleChoco97State.FromColdProcess();
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Choco97 = prior97, Choco98 = initialized };
        NativeBattleChocoControllerState view = NativeBattleChocoControllerState.Read(memory, true);
        Assert.Throws<InvalidDataException>(() => view.Io!.To97());
        NativeBattleEffectMemory written = view.Write(memory, true);
        Assert.Same(prior97, written.Choco97);
        Assert.Equal(0x120, written.Choco98!.Io!.Script.Length);
        Assert.Equal(0x152B2A8u, written.Choco98.Io.CursorAddress);
        Assert.Same(io.Banks, written.Choco98.Io.Banks);
        Assert.Null(written.Choco98.PendingIoBanks);
        NativeBattleChoco98State restored = JsonSerializer.Deserialize<NativeBattleChoco98State>(JsonSerializer.Serialize(written.Choco98))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(initialized), JsonSerializer.Serialize(restored));
        Assert.Throws<NotSupportedException>(() => (initialized with { PendingIoBanks = cold.PendingIoBanks }).Validate());
    }

    [Fact]
    public void Missing_warm_resources_and_wrong_scratch_are_rejected_without_cold_reconstruction()
    {
        NativeBattleChoco98State cold = NativeBattleChoco98State.FromColdProcess();
        Assert.Throws<NotSupportedException>(() => (cold with { ProgramSourceKnownCold = false }).Validate());
        Assert.Throws<NotSupportedException>(() => (cold with { IoScriptKnownCold = false }).Validate());
        Assert.Throws<InvalidDataException>(() => (cold with { MorphScratch = new byte[0x3C0].ToImmutableArray() }).Validate());
        Assert.Throws<InvalidDataException>(() => (cold with { ProjectedScratch = new byte[0xCD0 - 1].ToImmutableArray() }).Validate());
    }

    [Fact]
    public void Async98_completion_writes_only_actual_root_BYTE62_in_its_own_pool()
    {
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(NativeBattleChoco98Controller.MainDescriptor, NativeBattleChoco98Controller.MainBank, 0x64, 2)]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x7219D0, 0x64);
        NativeBattleOriginalEffectState effect = new(98, 97, new(98, 0x721860, 0x721840), new(0, 1, 8, 0, 0, 0), null) { Pools = root.Hierarchy.Pools };
        NativeBattleOriginalEffectState completed = NativeBattleChoco98Controller.WriteCompletionByte(effect, root.Address + 0x62, 1);
        Assert.Equal((byte)0, new NativeBattleEffectHierarchy(effect.Pools).Read(root.Address).ReadByte(0x62));
        Assert.Equal((byte)1, new NativeBattleEffectHierarchy(completed.Pools).Read(root.Address).ReadByte(0x62));
        Assert.Equal((byte)0, new NativeBattleEffectHierarchy(completed.Pools).Read(root.Address).ReadByte(0x63));
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco98Controller.WriteCompletionByte(effect, root.Address + 0x63, 1));
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco97Controller.WriteCompletionByte(effect, root.Address + 0x62, 1));
    }
}
