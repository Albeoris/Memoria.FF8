using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePreRootStackObserverTests
{
    [Fact]
    public void Last_actual_vitals_callback_supplies_the_native_common_frame_return_word()
    {
        NativeBattleStackBytes stack = NativeBattlePreRootStackObserver.BeginHostFrame(125);
        stack = NativeBattlePreRootStackObserver.ObserveUiWindowUpdate(stack, 0x4BB030, NativeBattleUiStackContext.NormalPreCore47CF60, "window1");
        Assert.Throws<NotSupportedException>(() => stack.IsNonzero(-0x8C));
        stack = NativeBattlePreRootStackObserver.ObserveUiWindowUpdate(stack, 0x4B0500, NativeBattleUiStackContext.NormalPreCore47CF60, "window7");
        stack = NativeBattlePreRootStackObserver.ObserveUiWindowUpdate(stack, 0, NativeBattleUiStackContext.NormalPreCore47CF60, "window8-empty");
        Assert.Equal(0x4B04E3u, stack.ReadUInt32(-0x8C));
        Assert.Equal(0x1A7904Cu, stack.ReadUInt32(-0x88));
        Assert.Equal(0x4B04DEu, stack.Observations[^1].Instruction);
        Assert.True(stack.IsNonzero(-0x8C));
    }

    [Fact]
    public void A_later_unmodeled_window_or_different_call_context_loses_the_word()
    {
        NativeBattleStackBytes stack = Vitals();
        NativeBattleStackBytes later = NativeBattlePreRootStackObserver.ObserveUiWindowUpdate(stack, 0x4B0000, NativeBattleUiStackContext.NormalPreCore47CF60, "later-window");
        Assert.Throws<NotSupportedException>(() => later.IsNonzero(-0x8C));
        Assert.Throws<NotSupportedException>(() => later.IsNonzero(-0x88));
        NativeBattleStackBytes other = NativeBattlePreRootStackObserver.ObserveUiWindowUpdate(stack, 0x4B0500, NativeBattleUiStackContext.Other, "other-callsite");
        Assert.Throws<NotSupportedException>(() => other.IsNonzero(-0x8C));
        Assert.Throws<NotSupportedException>(() => NativeBattlePreRootStackObserver.BeginHostFrame(126).IsNonzero(-0x8C));
    }

    [Fact]
    public void Entry_initializer_invalidates_UI_word_then_actual_spawn_CALL_supplies_new_word()
    {
        NativeBattleStackBytes stack = NativeBattlePreRootStackObserver.ObserveUnknownCall(Vitals(), 0x6CF4D0, "effect-entry");
        Assert.Throws<NotSupportedException>(() => stack.IsNonzero(-0x8C));
        stack = stack.StoreUInt32(-0x8C, 0x6CF68C, 0x6CF687, "root-target-spawn");
        Assert.Equal(0x6CF68Cu, stack.ReadUInt32(-0x8C));
        Assert.Equal(new uint[] { 0x4B04C6, 0x4B04DE, 0x6CF4D0, 0x6CF687 }, stack.Observations.Select(item => item.Instruction));
        Assert.Throws<NotSupportedException>(() => stack.IsNonzero(-0x88));
    }

    [Fact]
    public void Native_pending_and_priority_blocked_packets_preserve_but_dispatch_invalidates()
    {
        (NativeBattleDescriptorState state, int slot) = NativeBattleDescriptorState.Clear().Enqueue(104, 128, 0x1D280C4);
        state = state with { Depth = 1, Priorities = state.Priorities.SetItem(1, 128), Packets = state.Packets.SetItem(slot, state.Packets[slot] with { Bytes = state.Packets[slot].Bytes.SetItem(1, 136) }) };
        (state, _) = state.Enqueue(10, 128, 0x47DE70);
        NativeBattleStackBytes stack = NativeBattlePreRootStackObserver.ObserveDescriptorPump500CC0(Vitals(), state, "pending-action");
        Assert.Equal(0x4B04E3u, stack.ReadUInt32(-0x8C));
        Assert.Equal(0x50CFB1u, stack.ReadUInt32(-0x88));
        NativeBattleDescriptorState dispatch = state with { Depth = 0 };
        NativeBattleStackBytes unknown = NativeBattlePreRootStackObserver.ObserveDescriptorPump500CC0(stack, dispatch, "dispatch-callback");
        Assert.Throws<NotSupportedException>(() => unknown.IsNonzero(-0x8C));
        Assert.Throws<NotSupportedException>(() => unknown.IsNonzero(-0x88));
    }

    [Fact]
    public void Live_main_descriptor_selects_shallow_casting_poll_only_in_phase6()
    {
        NativeBattleStackBytes stack = NativeBattlePreRootStackObserver.ObserveCasting50A9A0(Vitals(), 6, true, "casting-poll");
        Assert.Equal(0x4B04E3u, stack.ReadUInt32(-0x8C));
        Assert.Equal(0x1A7904Cu, stack.ReadUInt32(-0x88));
        Assert.Throws<NotSupportedException>(() => NativeBattlePreRootStackObserver.ObserveCasting50A9A0(stack, 4, false, "invoke").IsNonzero(-0x8C));
        Assert.Throws<NotSupportedException>(() => NativeBattlePreRootStackObserver.ObserveCasting50A9A0(stack, 6, false, "finish").IsNonzero(-0x8C));
    }

    [Fact]
    public void Sentinel_deadline_and_retained_sequence_preserve_but_copy_or_release_invalidates()
    {
        byte[] payload = new byte[32];
        BinaryPrimitives.WriteUInt32LittleEndian(payload, 0x1D972C0);
        payload[9] = 11;
        NativeBattleAuxiliaryTask task = new(1, 0, null, NativeBattleAuxiliaryKind.TextureAnimation5057D0, payload.ToImmutableArray()) { Program = new([255], 0), UnknownByteMask = 0xF0 };
        NativeBattleGraphicsModel model = new(0, 3) { Sequence = 11, ResourceFlags44 = 34 };
        NativeBattleStackBytes stack = NativeBattlePreRootStackObserver.ObserveTextureAnimation5057D0(Vitals(), task, model, "texture-wait");
        Assert.Equal(0x4B04E3u, stack.ReadUInt32(-0x8C));
        Assert.Equal(0x1A7904Cu, stack.ReadUInt32(-0x88));
        NativeBattleAuxiliaryTask copy = task with { Program = new([1], 0) };
        Assert.Throws<NotSupportedException>(() => NativeBattlePreRootStackObserver.ObserveTextureAnimation5057D0(stack, copy, model, "copy").IsNonzero(-0x8C));
        Assert.Throws<NotSupportedException>(() => NativeBattlePreRootStackObserver.ObserveTextureAnimation5057D0(stack, task, model with { ResourceFlags44 = 0x40 }, "release").IsNonzero(-0x8C));
    }

    [Fact]
    public void Current_frame_observer_roundtrips_before_an_independent_next_call()
    {
        NativeBattleStackBytes first = Vitals();
        NativeBattleStackBytes second = JsonSerializer.Deserialize<NativeBattleStackBytes>(JsonSerializer.Serialize(first))!;
        first = NativeBattlePreRootStackObserver.ObserveCasting50A9A0(first, 6, true, "poll");
        second = NativeBattlePreRootStackObserver.ObserveCasting50A9A0(second, 6, true, "poll");
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }

    [Fact]
    public void Reached_critical_flash_preserves_the_word_but_other_module_callbacks_invalidate()
    {
        NativeBattleModuleTask flash = new(1, 0, null, NativeBattleModuleKind.CriticalFlash571300, ImmutableArray.CreateRange(new byte[24]));
        NativeBattleStackBytes stack = NativeBattlePreRootStackObserver.ObserveModuleCallback(Vitals(), flash, "flash");
        Assert.Equal(0x4B04E3u, stack.ReadUInt32(-0x8C));
        Assert.Equal(0x1A7904Cu, stack.ReadUInt32(-0x88));
        NativeBattleModuleTask unknown = flash with { Kind = NativeBattleModuleKind.Sprite570BC0 };
        Assert.Throws<NotSupportedException>(() => NativeBattlePreRootStackObserver.ObserveModuleCallback(stack, unknown, "sprite").IsNonzero(-0x8C));
        Assert.Throws<NotSupportedException>(() => NativeBattlePreRootStackObserver.ObserveModuleCallback(stack, flash with { Callback = 0x571300 }, "custom callback").IsNonzero(-0x8C));
    }

    [Fact]
    public void Empty_descriptor_pump_preserves_both_UI_words_and_the_next_host_frame_starts_unknown()
    {
        NativeBattleStackBytes stack = NativeBattlePreRootStackObserver.ObserveDescriptorPump500CC0(Vitals(), NativeBattleDescriptorState.Clear(), "empty-pump");
        Assert.Equal(0x4B04E3u, stack.ReadUInt32(-0x8C));
        Assert.Equal(0x1A7904Cu, stack.ReadUInt32(-0x88));
        NativeBattleStackBytes next = NativeBattlePreRootStackObserver.BeginHostFrame(126);
        Assert.NotEqual(stack.FrameOwner, next.FrameOwner);
        Assert.Throws<NotSupportedException>(() => next.IsNonzero(-0x8C));
        Assert.Throws<NotSupportedException>(() => next.IsNonzero(-0x88));
    }

    [Fact]
    public void An_observed_Dispel_operand_does_not_establish_the_adjacent_Hero_operand()
    {
        NativeBattleStackBytes stack = NativeBattlePreRootStackObserver.BeginHostFrame(125).StoreUInt32(-0x8C, 0x6CF68C, 0x6CF687, "Dispel workspace initializer");
        Assert.True(stack.IsNonzero(-0x8C));
        Assert.Throws<NotSupportedException>(() => stack.IsNonzero(-0x88));
    }
    private static NativeBattleStackBytes Vitals()
    {
        return NativeBattlePreRootStackObserver.ObserveUiWindowUpdate(NativeBattlePreRootStackObserver.BeginHostFrame(125), 0x4B0500, NativeBattleUiStackContext.NormalPreCore47CF60, "vitals");
    }
}
