using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshPresentationTests
{
    [Theory]
    [InlineData((ushort)327)]
    [InlineData((ushort)328)]
    [InlineData((ushort)329)]
    [InlineData((ushort)330)]
    public void Authored_flash_allocation_and_every_live_cold_callback_preserve_native_state(ushort effect)
    {
        (NativeBattleOriginalEffectState selected, NativeBattleGilgameshFlashSource source) = Owner(effect);
        NativeBattleModuleState native = NativeBattleModuleState.Clear().AppendFlash(1, 1, 1, 32);
        NativeBattleModuleState captured = native.EnterGilgameshPresentation(selected);
        native = native.AppendFlash(2, 2, 12, 255);
        captured = captured.AppendGilgameshFlash(effect, source, 279, 2, 2, 12, 255);
        int ownedSlot = captured.Tail!.Value;
        NativeBattleGilgameshFlashPresentation receipt = captured.Slots[ownedSlot].GilgameshFlashPresentation!;
        Assert.Null(captured.Slots[0].GilgameshFlashPresentation);
        Assert.Equal(source, receipt.Source);
        Assert.Equal(NativeJson(native), NativeJson(captured));
        int ownedDraws = 0;
        for (int callback = 0; callback < 17; callback++)
        {
            string frozen = JsonSerializer.Serialize(captured);
            NativeBattleModuleState cold = JsonSerializer.Deserialize<NativeBattleModuleState>(frozen)!.RestoreGilgameshPresentation();
            Assert.Equal(frozen, JsonSerializer.Serialize(cold));
            NativeBattleModuleStep a = native.Advance(new(0));
            NativeBattleModuleStep b = captured.Advance(new(0));
            NativeBattleModuleStep c = cold.Advance(new(0));
            Assert.Equal(NativeJson(a.State), NativeJson(b.State));
            Assert.Equal(JsonSerializer.Serialize(b.State), JsonSerializer.Serialize(c.State));
            Assert.Equal(a.Draws.Select(draw => draw.Intensity), b.Draws.Select(draw => draw.Intensity));
            Assert.Equal(b.Draws.Select(draw => draw.PilotIdentity), c.Draws.Select(draw => draw.PilotIdentity));
            foreach (NativeBattleModuleDraw draw in b.Draws.Where(draw => draw.GilgameshFlashPresentation is not null))
            {
                ownedDraws++;
                NativeBattleModuleDraw plain = a.Draws[b.Draws.IndexOf(draw)];
                NativeBattleModulePacketBatch expected = NativeBattleFlashPackets.Emit(plain);
                NativeBattleModulePacketBatch actual = NativeBattleFlashPackets.Emit(draw);
                Assert.Equal(expected.Primitives[0].Primitive.Words, actual.Primitives[0].Primitive.Words);
                Assert.Equal(expected.Primitives[0].Primitive.PhysicalWriteMask, actual.Primitives[0].Primitive.PhysicalWriteMask);
                NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(actual.Primitives[0].Primitive.PilotGeometry);
                Assert.Equal(effect, geometry.Identity.Effect);
                Assert.Equal(receipt.Identity, geometry.Identity);
                Assert.True(geometry.ScreenSpace);
                Assert.False(geometry.Continuous);
                Assert.Equal(System.Numerics.Vector4.Zero, geometry.SourceDepths);
            }
            native = a.State;
            captured = b.State;
        }
        Assert.Equal(16, ownedDraws);
        Assert.Null(captured.Head);
        (NativeBattleModuleState reused, int slot) = captured.Append(NativeBattleModuleKind.CriticalFlash571300);
        Assert.Equal(0, slot);
        Assert.Null(reused.Slots[slot].GilgameshFlashPresentation);
        Assert.Null(reused.Slots[slot].PilotIdentity);
        (reused, slot) = reused.AppendCallback(0x1234);
        Assert.Equal(ownedSlot, slot);
        Assert.Null(reused.Slots[slot].GilgameshFlashPresentation);
        Assert.Null(reused.Slots[slot].PilotIdentity);
    }

    [Theory]
    [InlineData((ushort)327)]
    [InlineData((ushort)328)]
    [InlineData((ushort)329)]
    [InlineData((ushort)330)]
    public void Continued_flash_renders_but_never_claims_a_different_or_new_same_effect_context(ushort effect)
    {
        (NativeBattleOriginalEffectState selected, NativeBattleGilgameshFlashSource source) = Owner(effect);
        NativeBattleModuleState state = NativeBattleModuleState.Clear().EnterGilgameshPresentation(selected).AppendGilgameshFlash(effect, source, 279, 2, 2, 12, 255);
        NativeBattleModuleDraw draw = Assert.Single(state.Advance(new(0)).Draws);
        NativeBattleModulePacketBatch batch = NativeBattleFlashPackets.Emit(draw);
        NativeBattleGilgameshFlashPresentation receipt = draw.GilgameshFlashPresentation!;
        NativeBattleOriginalEffectState same = state.RecordGilgameshFlashPackets(selected, receipt, "off_B6D070", 0, 52, batch)!;
        Assert.Equal(new NativeBattlePrimitiveReference("off_B6D070", 0, 52), Assert.Single(same.PresentationPackets));
        NativeBattleOriginalEffectState other = new(0, 0, new(0, 0, 0), null, 0);
        Assert.Same(other, state.RecordGilgameshFlashPackets(other, receipt, "off_B6D070", 0, 52, batch));
        Assert.Null(state.RecordGilgameshFlashPackets(null, receipt, "off_B6D070", 0, 52, batch));
        NativeBattleOriginalEffectState pending = selected with { ReturnedDescriptor = null, Pools = [] };
        Assert.Same(pending, state.RecordGilgameshFlashPackets(pending, receipt, "off_B6D070", 0, 52, batch));
        NativeBattleModuleState next = state.EnterGilgameshPresentation(Owner(effect).Selected);
        Assert.Same(selected, next.RecordGilgameshFlashPackets(selected, receipt, "off_B6D070", 0, 52, batch));
        NativeBattleModuleDraw tail = Assert.Single(next.RestoreGilgameshPresentation().Advance(new(0)).Draws);
        Assert.Equal(batch.Primitives[0].Primitive.Words, NativeBattleFlashPackets.Emit(tail).Primitives[0].Primitive.Words);
        Assert.Equal(receipt, tail.GilgameshFlashPresentation);
    }

    [Theory]
    [InlineData((ushort)327)]
    [InlineData((ushort)328)]
    [InlineData((ushort)329)]
    [InlineData((ushort)330)]
    public void Executed_director_counter_is_forwarded_only_for_opted_authored_flash(ushort effect)
    {
        NativeBattleGilgameshFlashSource source = Owner(effect).Source;
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x21FFD58, 0x21FFD68, 36, 100);
        (pool, int slot) = pool.Append(0x592300);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(12, 279);
        NativeBattleModulePoolTask nativeTask = NativeBattleTestSnapshots.Independent(task);
        NativeBattleModulePoolTask capturedTask = NativeBattleTestSnapshots.Independent(task);
        List<NativeBattleGilgameshDirectorOperation> native = [];
        List<NativeBattleGilgameshDirectorOperation> captured = [];
        NativeBattleGilgameshDirectorStep a = NativeBattleGilgameshDirector.Advance592300(nativeTask, new(0, () => { }, () => 0, native.Add) { Effect = effect });
        NativeBattleGilgameshDirectorStep b = NativeBattleGilgameshDirector.Advance592300(capturedTask, new(0, () => { }, () => 0, captured.Add) { Effect = effect, CapturePresentation = true });
        Assert.Equal(279, task.ReadWord(12));
        Assert.Equal(280, a.Task.ReadWord(12));
        Assert.Equal(280, b.Task.ReadWord(12));
        Assert.Equal(JsonSerializer.Serialize(a with { Task = NativeBattleStorageSnapshots.Capture(a.Task) }), JsonSerializer.Serialize(b with { Task = NativeBattleStorageSnapshots.Capture(b.Task) }));
        Assert.Equal(JsonSerializer.Serialize(native), JsonSerializer.Serialize(captured));
        Assert.All(native, operation => Assert.Null(operation.PresentationCounter));
        NativeBattleGilgameshDirectorOperation flash = Assert.Single(captured, operation => operation.Service == NativeBattleGilgameshDirectorService.QueueColor);
        Assert.Equal(279, flash.PresentationCounter);
        Assert.Equal(new[] { 2, 2, 12, 255 }, new[] { flash.First, flash.Second, flash.Third, flash.Fourth });
        Assert.Equal((uint)0x592300, source.Callback);
    }

    [Theory]
    [InlineData((ushort)327)]
    [InlineData((ushort)328)]
    [InlineData((ushort)329)]
    [InlineData((ushort)330)]
    public void Independent_entry_and_later_source_creation_use_identical_serialized_birth_ordinals(ushort effect)
    {
        var warmOwner = Owner(effect);
        var coldOwner = Owner(effect);
        long warmNative = NativeBattlePilotVisualLifetimes.Identity(effect, warmOwner.Selected.Pools[0].Slots[0]).Generation;
        long coldNative = NativeBattlePilotVisualLifetimes.Identity(effect, coldOwner.Selected.Pools[0].Slots[0]).Generation;
        Assert.NotEqual(warmNative, coldNative);
        NativeBattleModuleState warm = NativeBattleModuleState.Clear().EnterGilgameshPresentation(warmOwner.Selected);
        NativeBattleModuleState cold = JsonSerializer.Deserialize<NativeBattleModuleState>(JsonSerializer.Serialize(NativeBattleModuleState.Clear()))!.EnterGilgameshPresentation(coldOwner.Selected);
        Assert.Equal(JsonSerializer.Serialize(warm), JsonSerializer.Serialize(cold));
        Assert.Equal(1, warm.GilgameshPresentation!.Context.Birth);
        Assert.Equal(2, warm.GilgameshPresentation.Context.RootGeneration);
        Assert.Equal(3, warm.GilgameshPresentation.Context.DirectorGeneration);
        warm = warm.AppendGilgameshFlash(effect, warmOwner.Source, 279, 2, 2, 12, 255);
        cold = cold.RestoreGilgameshPresentation().AppendGilgameshFlash(effect, coldOwner.Source, 279, 2, 2, 12, 255);
        Assert.Equal(JsonSerializer.Serialize(warm), JsonSerializer.Serialize(cold));
        Assert.Equal(JsonSerializer.Serialize(warm.Advance(new(0)).State), JsonSerializer.Serialize(cold.Advance(new(0)).State));
    }

    [Theory]
    [InlineData((ushort)327)]
    [InlineData((ushort)328)]
    [InlineData((ushort)329)]
    [InlineData((ushort)330)]
    public void Deferred_module_flash_is_recorded_after_root_poll_clears_only_the_previous_frame(ushort effect)
    {
        var owner = Owner(effect);
        NativeBattleModuleState modules = NativeBattleModuleState.Clear().EnterGilgameshPresentation(owner.Selected).AppendGilgameshFlash(effect, owner.Source, 279, 2, 2, 12, 255);
        NativeBattleModuleStep advanced = modules.Advance(new(0));
        NativeBattleModuleDraw draw = Assert.Single(advanced.Draws);
        NativeBattlePrimitiveReference obsolete = new("off_B6D070", 0, 80);
        NativeBattleOriginalEffectState finished = owner.Selected with { ReturnedDescriptor = 0, Pools = owner.Selected.Pools.Select(pool => pool.Advance((current, _) => new(current, 2)).Pool).ToImmutableArray(), PresentationPackets = [obsolete] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { ReturnedTaskPointer1D96AAC = 0 };
        BattleSession session = (BattleSession)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(BattleSession));
        typeof(BattleSession).GetField("nativeEffectMemory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(session, memory);
        NativeBattleGraphicsState graphics = new([], [], 0, 1) { OriginalEffect = finished, ModuleEffects = advanced.State, ModuleDraws = advanced.Draws };
        NativeBattleGraphicsState polled = (NativeBattleGraphicsState)typeof(BattleSession).GetMethod("AdvanceNativeOriginalEffect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(session, [graphics])!;
        Assert.Empty(polled.OriginalEffect!.PresentationPackets);
        Assert.Contains(obsolete, finished.PresentationPackets);
        NativeBattleModulePacketBatch batch = NativeBattleFlashPackets.Emit(draw);
        NativeBattleOriginalEffectState recorded = advanced.State.RecordGilgameshFlashPackets(polled.OriginalEffect, draw.GilgameshFlashPresentation!, "off_B6D070", 0, 52, batch)!;
        Assert.Equal(new NativeBattlePrimitiveReference("off_B6D070", 0, 52), Assert.Single(recorded.PresentationPackets));
        Assert.DoesNotContain(obsolete, recorded.PresentationPackets);
        Assert.Same(memory, session.NativeEffectMemory);
    }

    [Fact]
    public void Missing_active_context_and_tampered_or_reused_slot_provenance_fail_explicitly()
    {
        (NativeBattleOriginalEffectState selected, NativeBattleGilgameshFlashSource source) = Owner(327);
        NativeBattleModuleState fresh = NativeBattleModuleState.Clear();
        string original = JsonSerializer.Serialize(fresh);
        Assert.DoesNotContain("GilgameshPresentation", original);
        Assert.DoesNotContain("GilgameshFlashPresentation", original);
        Assert.Throws<NotSupportedException>(() => fresh.RequireGilgameshPresentation(selected));
        NativeBattleModuleState owned = fresh.EnterGilgameshPresentation(selected).AppendGilgameshFlash(327, source, 279, 2, 2, 12, 255);
        int slot = owned.Tail!.Value;
        NativeBattleGilgameshFlashPresentation receipt = owned.Slots[slot].GilgameshFlashPresentation!;
        Assert.Throws<InvalidDataException>(() => (owned with { GilgameshPresentation = owned.GilgameshPresentation! with { NextBirth = receipt.Birth } }).RestoreGilgameshPresentation());
        Assert.Throws<InvalidDataException>(() => (owned with { Slots = owned.Slots.SetItem(slot, owned.Slots[slot] with { GilgameshFlashPresentation = receipt with { Counter = 278 } }) }).RestoreGilgameshPresentation());
        Assert.Throws<InvalidDataException>(() => (owned with { Slots = owned.Slots.SetItem(slot, owned.Slots[slot] with { GilgameshFlashPresentation = receipt with { Identity = receipt.Identity with { ResourceGeneration = receipt.Identity.ResourceGeneration + 1 } } }) }).RestoreGilgameshPresentation());
        Assert.Throws<InvalidDataException>(() => (owned with { Slots = owned.Slots.SetItem(slot, owned.Slots[slot] with { Kind = NativeBattleModuleKind.Placement570CC0 }) }).RestoreGilgameshPresentation());
        Assert.Equal(original, JsonSerializer.Serialize(fresh));
    }

    private static (NativeBattleOriginalEffectState Selected, NativeBattleGilgameshFlashSource Source) Owner(ushort effect)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x21FF4A8, 0x21FF468, 16, 2);
        (root, _) = root.Append(0x596B70);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x21FFD58, 0x21FFD68, 36, 100);
        (children, int slot) = children.Append(0x592300);
        NativeBattleEffectTaskPool particles = NativeBattleEffectTaskPool.Initialize(0x21FF458, 0x21FF3C8, 36, 4);
        NativeBattleGilgameshFlashSource source = new(children.BankAddress + (uint)(slot * children.Stride), children.Slots[slot].Callback);
        NativeBattleOriginalEffectState selected = new(effect, effect - 1, new(effect, NativeBattleGilgameshProfile.ForEffect(effect).Entry, 0), null, root.DescriptorAddress) { Pools = [root, children, particles] };
        return (selected, source);
    }

    private static string NativeJson(NativeBattleModuleState state)
    {
        return JsonSerializer.Serialize(state with { GilgameshPresentation = null, Slots = state.Slots.Select(task => task with { GilgameshFlashPresentation = null }).ToImmutableArray() });
    }
}
