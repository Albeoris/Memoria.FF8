using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCastingModuleTests
{
    [Fact]
    public void A5_override_selects_original_table_entry1_and_clears_only_its_graphics_bit()
    {
        Assert.Equal(((byte)1, 0x80000010u), NativeBattleModuleState.SelectModelEffect(255, 0xC0000010));
        Assert.Equal(((byte)255, 0x80000010u), NativeBattleModuleState.SelectModelEffect(255, 0x80000010));
        Assert.Throws<NotSupportedException>(() => NativeBattleModuleState.Clear().AppendModelEffect(0, 6));
    }

    [Fact]
    public void Original574C80_appends_all_twelve_children_in_the_same_pass_and_consumes35_crt_draws()
    {
        NativeCrtRandomState initial = new(7);
        NativeBattleModuleStep first = NativeBattleModuleState.Clear().AppendModelEffect(0, 0).Advance(Inputs(initial));
        Assert.Equal(13, first.State.PublishedCount);
        Assert.Equal(5, first.State.Slots.Count(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleModuleKind.CastingRing5753A0));
        Assert.Equal(6, first.State.Slots.Count(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleModuleKind.CastingParticle574F50));
        Assert.Equal(new ushort[] { 5, 6 }, first.Draws.Where(draw => draw.Kind == NativeBattleModuleDrawKind.Sound).Select(draw => draw.Sound));
        Assert.Contains(first.Draws, draw => draw.Kind == NativeBattleModuleDrawKind.Mesh && draw.MeshId == 1 && draw.MeshClut == 0x3867 && draw.MeshScale!.Value.Y == 0);
        Assert.Contains(first.Draws, draw => draw.Kind == NativeBattleModuleDrawKind.Mesh && draw.MeshId == 7 && draw.Anchor!.Y == 123);
        NativeCrtRandomState expected = initial;
        for (int draw = 0; draw < 35; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, first.CrtRandomAfter);
        NativeBattleModuleStep paused = first.State.Advance(Inputs(expected) with { GraphicsFlags = 1 });
        Assert.Equal(JsonSerializer.Serialize(first.State), JsonSerializer.Serialize(paused.State));
        Assert.Equal(expected, paused.CrtRandomAfter);
        Assert.DoesNotContain(paused.Draws, draw => draw.Kind == NativeBattleModuleDrawKind.Sound);
        Assert.Contains(paused.Draws, draw => draw.Kind == NativeBattleModuleDrawKind.Mesh);
    }

    [Fact]
    public void Casting_children_restore_raw_payloads_and_release_by_their_original_counter_contracts()
    {
        NativeBattleModuleState state = NativeBattleModuleState.Clear().AppendModelEffect(0, 0);
        NativeCrtRandomState random = new(17);
        int callbacks = 0;
        while (state.Head is not null && callbacks < 40)
        {
            NativeBattleModuleState restored = JsonSerializer.Deserialize<NativeBattleModuleState>(JsonSerializer.Serialize(state))!;
            NativeBattleModuleInputs inputs = Inputs(random);
            NativeBattleModuleStep step = state.Advance(inputs);
            NativeBattleModuleStep repeated = restored.Advance(inputs);
            Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(repeated));
            state = step.State;
            random = step.CrtRandomAfter!;
            callbacks++;
        }
        Assert.Equal(31, callbacks);
        Assert.Null(state.Head);
        Assert.Equal(0, state.PublishedCount);
        NativeCrtRandomState expected = new(17);
        for (int draw = 0; draw < 1115; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, random);
    }

    [Fact]
    public void Authored18_provenance_preserves_all_native_casting_callbacks_CRT_sounds_and_cold_next_states()
    {
        NativeBattleModuleState plain = NativeBattleModuleState.Clear().AppendModelEffect(0, 0);
        NativeBattleModuleState owned = OwnedCasting();
        NativeCrtRandomState random = new(17);
        int callbacks = 0;
        int reusedReceipts = 0;
        while (plain.Head is not null && callbacks < 40)
        {
            owned = owned.RestoreCastingPresentation(_ => 30);
            NativeBattleModuleState cold = JsonSerializer.Deserialize<NativeBattleModuleState>(JsonSerializer.Serialize(owned))!.RestoreCastingPresentation(_ => 30);
            Assert.Equal(JsonSerializer.Serialize(owned), JsonSerializer.Serialize(cold));
            NativeBattleModuleStep native = plain.Advance(Inputs(random));
            NativeBattleModuleStep captured = owned.Advance(Inputs(random));
            NativeBattleModuleStep repeated = cold.Advance(Inputs(random));
            Assert.Equal(JsonSerializer.Serialize(native), JsonSerializer.Serialize(captured with { State = captured.State with { CastingPresentation = null } }));
            Assert.Equal(JsonSerializer.Serialize(captured), JsonSerializer.Serialize(repeated));
            foreach (NativeBattleModuleDraw draw in captured.Draws.Where(draw => draw.Kind is NativeBattleModuleDrawKind.Mesh or NativeBattleModuleDrawKind.Sprite))
            {
                NativeBattleCastingTaskProvenance receipt = Assert.IsType<NativeBattleCastingTaskProvenance>(draw.PresentationSource);
                Assert.Equal((ushort)18, draw.PresentationOwnerEffect);
                Assert.Equal(draw.PresentationSourceSlot, receipt.Identity.Slot);
                Assert.Equal(draw.PresentationSourceKind, receipt.Kind);
                Assert.True(receipt.Identity.Generation > receipt.Source.Identity.Generation);
                Assert.Equal(0x90, receipt.Source.ScriptOffset);
                Assert.Equal(30, receipt.Source.Sequence);
                Assert.NotNull(draw.PilotIdentity);
                if (captured.State.CastingPresentation!.Tasks.TryGetValue(receipt.Identity.Slot, out NativeBattleCastingTaskProvenance? current) && current.Identity != receipt.Identity)
                {
                    reusedReceipts++;
                }
            }
            plain = native.State;
            owned = captured.State;
            random = native.CrtRandomAfter!;
            callbacks++;
        }
        Assert.Equal(31, callbacks);
        Assert.Null(owned.Head);
        Assert.True(reusedReceipts > 0, "Actual freed-slot reuse must retain the earlier callback's immutable draw receipt.");
        NativeCrtRandomState expected = new(17);
        for (int draw = 0; draw < 1115; draw++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, random);
    }

    [Fact]
    public void Explicit18_sidecar_is_optional_and_does_not_claim_an_unrelated_concurrent_cast()
    {
        NativeBattleModuleState original = NativeBattleModuleState.Clear().AppendModelEffect(1, 0);
        Assert.DoesNotContain("CastingPresentation", JsonSerializer.Serialize(original));
        NativeBattleModuleState context = original.EnterCinematic18Presentation();
        Assert.NotNull(context.CastingPresentation!.Cinematic18Birth);
        Assert.Empty(context.CastingPresentation.Tasks);
        NativeBattleModuleStep advanced = context.RestoreCastingPresentation(_ => 1).Advance(Inputs(new(7)));
        Assert.Empty(advanced.State.CastingPresentation!.Tasks);
        Assert.All(advanced.Draws, draw =>
        {
            Assert.Null(draw.PresentationOwnerEffect);
            Assert.Null(draw.PilotIdentity);
        });
        Assert.Equal(JsonSerializer.Serialize(original.Advance(Inputs(new(7)))), JsonSerializer.Serialize(advanced with { State = advanced.State with { CastingPresentation = null } }));
    }

    [Fact]
    public void Explicit18_provenance_validates_native_slot_kind_resource_and_reallocation_without_family_recovery()
    {
        NativeBattleModuleState state = OwnedCasting();
        NativeBattleCastingTaskProvenance receipt = state.CastingPresentation!.Tasks[0];
        NativeBattleModuleState bound = state.BindCastingResource(0, receipt, 0x1234);
        Assert.Equal(0x1234u, bound.CastingPresentation!.Tasks[0].ResourceAddress);
        Assert.Throws<InvalidDataException>(() => bound.BindCastingResource(0, bound.CastingPresentation.Tasks[0], 0x5678));
        NativeBattleModuleState wrong = state with { Slots = state.Slots.SetItem(0, state.Slots[0] with { Kind = NativeBattleModuleKind.Defensive5705A0 }) };
        Assert.Throws<InvalidDataException>(() => wrong.RestoreCastingPresentation(_ => 30));
        NativeBattleModuleState released = state with { Slots = state.Slots.SetItem(0, state.Slots[0] with { Flags = 0 }), Head = null, Tail = null };
        (NativeBattleModuleState reused, int slot) = released.Append(NativeBattleModuleKind.CastingRing5753A0);
        Assert.Equal(0, slot);
        Assert.False(reused.CastingPresentation!.Tasks.ContainsKey(slot));
        Assert.Null(reused.Slots[slot].PilotIdentity);
        Assert.Null(reused.Slots[slot].PresentationOwnerEffect);
        Assert.Empty(reused.RestoreCastingPresentation(_ => 30).CastingPresentation!.Tasks);
    }

    [Fact]
    public void Actual18_casting_sprite_receipt_preserves_native_packet_bytes_spacing_depth_and_resource()
    {
        NativeBattleModuleState state = OwnedCasting();
        NativeCrtRandomState random = new(7);
        NativeBattleModuleDraw? selected = null;
        for (int callback = 0; callback < 20 && selected is null; callback++)
        {
            NativeBattleModuleStep step = state.RestoreCastingPresentation(_ => 30).Advance(Inputs(random) with { Resources = [VisibleCastingSprite()] });
            selected = step.Draws.FirstOrDefault(draw => draw.Kind == NativeBattleModuleDrawKind.Sprite);
            state = step.State;
            random = step.CrtRandomAfter!;
        }
        Assert.NotNull(selected);
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 4096 };
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattleModulePacketBatch plain = NativeBattleModulePackets.EmitSprite571DC0(selected! with { PilotIdentity = null }, projection, 0, 17, 2);
        NativeBattleModulePacketBatch captured = NativeBattleModulePackets.EmitSprite571DC0(selected!, projection, 0, 17, 2);
        Assert.Equal(JsonSerializer.Serialize(plain), JsonSerializer.Serialize(captured));
        NativeBattleAllocatedPrimitive primitive = Assert.Single(captured.Primitives);
        NativeBattlePilotVisualGeometry visual = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.Primitive.PilotGeometry);
        Assert.Equal((ushort)18, visual.Identity.Effect);
        Assert.Equal(selected!.PilotIdentity!.Value.Generation, visual.Identity.Generation);
        Assert.NotNull(visual.Sprite);
        Assert.Equal((byte)4, visual.CornerCount);
        Assert.Equal(selected.Resource!.Address, VisibleCastingSprite().Address);
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(2L)]
    [InlineData(3L)]
    public void Retained18_next_birth_must_exceed_every_actual_context_source_parent_and_task(long next)
    {
        NativeBattleModuleState state = OwnedCasting();
        NativeBattleModuleState malformed = state with { CastingPresentation = state.CastingPresentation! with { NextBirth = next } };
        Assert.Throws<InvalidDataException>(() => malformed.RestoreCastingPresentation(_ => 30));
    }

    [Fact]
    public void Executed18_sequence_source_survives_cold_before_the_real_A5_seed_allocation()
    {
        NativeBattleModuleState bound = BoundSequence();
        NativeBattleModuleState cold = JsonSerializer.Deserialize<NativeBattleModuleState>(JsonSerializer.Serialize(bound))!;
        Assert.Empty(cold.CastingPresentation!.Tasks);
        NativeBattleModuleState direct = bound.AppendModelEffect(0, 0, bound.CastingPresentation!.Sequences[0]);
        NativeBattleModuleState restored = cold.AppendModelEffect(0, 0, cold.CastingPresentation.Sequences[0]);
        Assert.Equal(JsonSerializer.Serialize(direct), JsonSerializer.Serialize(restored));
        Assert.Equal(3, restored.CastingPresentation!.Tasks[0].Identity.Generation);
        Assert.Equal(1, restored.CastingPresentation.Tasks[0].Source.ContextBirth);
        Assert.Equal(0x20DFB48u, restored.CastingPresentation.Tasks[0].Source.ScriptAddress);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleModuleState.Clear().AppendModelEffect(0, 0)), JsonSerializer.Serialize(restored with { CastingPresentation = null }));
    }

    [Fact]
    public void Accepted18_tail_packets_are_owned_only_by_the_same_current_component_context()
    {
        NativeBattleModuleState state = OwnedCasting();
        NativeCrtRandomState random = new(7);
        NativeBattleModuleDraw? receiptDraw = null;
        for (int callback = 0; callback < 20 && receiptDraw is null; callback++)
        {
            NativeBattleModuleStep step = state.RestoreCastingPresentation(_ => 30).Advance(Inputs(random) with { Resources = [VisibleCastingSprite()] });
            receiptDraw = step.Draws.FirstOrDefault(draw => draw.Kind == NativeBattleModuleDrawKind.Sprite);
            state = step.State;
            random = step.CrtRandomAfter!;
        }
        Assert.NotNull(receiptDraw);
        NativeBattleCastingTaskProvenance receipt = receiptDraw!.PresentationSource!;
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 4096 };
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattleModulePacketBatch accepted = NativeBattleModulePackets.EmitSprite571DC0(receiptDraw with { PilotIdentity = null }, projection, 0, 17, 2);
        NativeBattleModulePacketBatch rendered = NativeBattleModulePackets.EmitSprite571DC0(receiptDraw, projection, 0, 17, 2);
        Assert.Equal(JsonSerializer.Serialize(accepted), JsonSerializer.Serialize(rendered));
        Assert.Equal((ushort)18, Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(rendered.Primitives).Primitive.PilotGeometry).Identity.Effect);
        Assert.NotEmpty(accepted.Primitives);
        Assert.All(accepted.Primitives, item => Assert.Null(item.Primitive.PilotGeometry));
        NativeBattleOriginalEffectState current18 = new(18, 17, new(18, 0, 0), null, 1);
        NativeBattleOriginalEffectState other = new(141, 140, new(141, 0, 0), null, 1);
        long context = state.CastingPresentation!.Cinematic18Birth!.Value;
        NativeBattleOriginalEffectState owned = NativeBattleControl18Presentation.RecordCurrentPackets(current18, context, receipt, "off_B6D070", 0, 128, accepted)!;
        Assert.Equal(new NativeBattlePrimitiveReference("off_B6D070", 0, 128), Assert.Single(owned.PresentationPackets));
        Assert.Same(other, NativeBattleControl18Presentation.RecordCurrentPackets(other, context, receipt, "off_B6D070", 0, 128, accepted));
        NativeBattleModuleState newer = state.EnterCinematic18Presentation();
        Assert.Same(current18, NativeBattleControl18Presentation.RecordCurrentPackets(current18, newer.CastingPresentation!.Cinematic18Birth, receipt, "off_B6D070", 0, 128, accepted));
        Assert.Null(NativeBattleControl18Presentation.RecordCurrentPackets(null, context, receipt, "off_B6D070", 0, 128, accepted));
        Assert.Equal(JsonSerializer.Serialize(current18), JsonSerializer.Serialize(owned));
        Assert.Equal(receipt, receiptDraw.PresentationSource);
    }

    private static NativeBattleModuleState OwnedCasting()
    {
        NativeBattleModuleState state = BoundSequence();
        return state.AppendModelEffect(0, 0, state.CastingPresentation!.Sequences[0]);
    }

    private static NativeBattleModuleState BoundSequence()
    {
        NativeBattleModuleState state = NativeBattleModuleState.Clear().EnterCinematic18Presentation();
        NativeBattleCastingSequenceProvenance source = new(new(18, 0x20DFAB8, 0, 123, 0), 0x20DFB48, 0x20DFAB8, 0x90, 0, 30, state.CastingPresentation!.Cinematic18Birth!.Value);
        return state.BindCastingSequence(0, source);
    }

    private static NativeBattleModuleResource VisibleCastingSprite()
    {
        byte[] bytes = new byte[272];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 10);
        for (int phase = 0; phase < 10; phase++)
        {
            int offset = 32 + phase * 24;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + phase * 2), (ushort)offset);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), 1);
            bytes[offset + 4] = 16;
            bytes[offset + 5] = 128;
            bytes[offset + 6] = 8;
            bytes[offset + 7] = 0x2C;
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(30), ushort.MaxValue);
        return new(5, 0x123400, 0, 0, bytes.ToImmutableArray());
    }

    private static NativeBattleModuleInputs Inputs(NativeCrtRandomState random)
    {
        return new(0) { CrtRandom = random, ReadModelAnchor = _ => new(10, 20, 30, 40), ReadModelTransform = _ => new(new(0, 0, 0), new(0, 0, 0), 0, 123, 4096), Resources = [Sprites()] };
    }

    private static NativeBattleModuleResource Sprites()
    {
        byte[] bytes = new byte[72];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 10);
        for (int phase = 0; phase < 10; phase++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + phase * 2), (ushort)(32 + phase * 4));
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(30), ushort.MaxValue);
        return new(5, 1, 0x570CC0, 0x570BC0, bytes.ToImmutableArray());
    }
}
