using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshFlashSource(uint TaskAddress, uint Callback);
public sealed record NativeBattleGilgameshPresentationContext(ushort Effect, long Birth, uint RootAddress, long RootGeneration, uint DirectorAddress, long DirectorGeneration);
public sealed record NativeBattleGilgameshPresentationState(long NextBirth, NativeBattleGilgameshPresentationContext Context);
public sealed record NativeBattleGilgameshFlashPresentation(ushort Effect, long ContextBirth, long Birth, int Slot, NativeBattleGilgameshFlashSource Source, int Counter, NativeBattlePilotVisualIdentity Identity)
{
    public long SourceBirth { get; init; }
}

public sealed partial record NativeBattleModuleTask
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleGilgameshFlashPresentation? GilgameshFlashPresentation { get; init; }
}

public sealed partial record NativeBattleModuleDraw
{
    [JsonIgnore]
    public NativeBattleGilgameshFlashPresentation? GilgameshFlashPresentation { get; init; }
}

public sealed partial record NativeBattleModuleState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleGilgameshPresentationState? GilgameshPresentation { get; init; }

    internal static bool OwnsGilgameshPresentation(ushort effect) => effect is 327 or 328 or 329 or 330;

    internal NativeBattleModuleState EnterGilgameshPresentation(NativeBattleOriginalEffectState selected)
    {
        if (!OwnsGilgameshPresentation(selected.Binding.Effect) || selected.Pools.Length != 3 || selected.Pools[0].Head is not int slot)
        {
            throw new InvalidDataException("Gilgamesh presentation entry requires its actual selected returned task allocation.");
        }
        NativeBattleEffectTaskPool pool = selected.Pools[0];
        NativeBattleModulePoolTask root = pool.Slots[slot];
        if (pool.DescriptorAddress != 0x21FF4A8 || pool.BankAddress != 0x21FF468 || pool.Stride != 16 || pool.Slots.Length != 2 || selected.ReturnedDescriptor != pool.DescriptorAddress || (root.Flags & 1) == 0 || root.Callback != 0x596B70)
        {
            throw new InvalidDataException("Gilgamesh presentation entry requires its actual returned callback.");
        }
        ValidateGilgameshPresentation();
        long birth = GilgameshPresentation?.NextBirth ?? 1;
        NativeBattleEffectTaskPool children = selected.Pools[1];
        if (children.DescriptorAddress != 0x21FFD58 || children.BankAddress != 0x21FFD68 || children.Stride != 36 || children.Slots.Length != 100)
        {
            throw new InvalidDataException("Gilgamesh presentation entry requires its actual director task bank.");
        }
        int directorSlot = Enumerable.Range(0, children.Slots.Length).Single(index => (children.Slots[index].Flags & 1) != 0 && children.Slots[index].Callback == 0x592300);
        NativeBattleGilgameshPresentationContext context = new(selected.Binding.Effect, birth, checked(pool.BankAddress + (uint)(slot * pool.Stride)), checked(birth + 1), checked(children.BankAddress + (uint)(directorSlot * children.Stride)), checked(birth + 2));
        return this with { GilgameshPresentation = new(checked(birth + 3), context) };
    }

    internal void RequireGilgameshPresentation(NativeBattleOriginalEffectState selected)
    {
        if (GilgameshPresentation?.Context is not { } context || context.Effect != selected.Binding.Effect)
        {
            throw new NotSupportedException("An active Gilgamesh component requires its explicit presentation context; migrate the selected older snapshot before use.");
        }
        ValidateGilgameshPresentation();
        if (selected.Pools.Length != 3 || selected.Pools[0].Head is not int slot || checked(selected.Pools[0].BankAddress + (uint)(slot * selected.Pools[0].Stride)) != context.RootAddress)
        {
            throw new InvalidDataException("Gilgamesh presentation context differs from its actual returned task owner.");
        }
    }

    internal NativeBattleModuleState AppendGilgameshFlash(ushort effect, NativeBattleGilgameshFlashSource source, int counter, short rise, short hold, short fall, short intensity)
    {
        NativeBattleGilgameshPresentationState presentation = GilgameshPresentation ?? throw new InvalidDataException("Gilgamesh flash requires its executed presentation context.");
        if (presentation.Context.Effect != effect || !OwnsGilgameshPresentation(effect) || source.Callback != 0x592300 || counter != 279 || rise != 2 || hold != 2 || fall != 12 || intensity != 255 || source.TaskAddress != presentation.Context.DirectorAddress)
        {
            throw new InvalidDataException("Gilgamesh flash requires the actual authored director source and native flash operands.");
        }
        ValidateGilgameshPresentation();
        NativeBattleModuleState allocated = AppendFlash(rise, hold, fall, intensity);
        int slot = allocated.Tail ?? throw new InvalidDataException("Gilgamesh flash allocation did not publish its actual slot.");
        long birth = presentation.NextBirth;
        NativeBattlePilotVisualIdentity identity = new(effect, DescriptorAddress, slot, birth, unchecked((int)source.Callback)) { ResourceGeneration = GilgameshFlashResource(presentation.Context.Birth, presentation.Context.DirectorGeneration, source, counter) };
        NativeBattleGilgameshFlashPresentation receipt = new(effect, presentation.Context.Birth, birth, slot, source, counter, identity) { SourceBirth = presentation.Context.DirectorGeneration };
        NativeBattleModuleTask child = allocated.Slots[slot];
        return allocated with { GilgameshPresentation = presentation with { NextBirth = checked(birth + 1) }, Slots = allocated.Slots.SetItem(slot, child with { GilgameshFlashPresentation = receipt, PilotIdentity = identity }) };
    }

    internal NativeBattleModuleState RestoreGilgameshPresentation()
    {
        ValidateGilgameshPresentation();
        NativeBattleModuleState state = this;
        for (int slot = 0; slot < Slots.Length; slot++)
        {
            NativeBattleModuleTask task = state.Slots[slot];
            if ((task.Flags & 1) != 0 && task.GilgameshFlashPresentation is { } receipt)
            {
                state = state with { Slots = state.Slots.SetItem(slot, task with { PilotIdentity = receipt.Identity }) };
            }
        }
        return state;
    }

    internal ImmutableArray<NativeBattleModuleDraw> BindGilgameshPresentationDraws(int slot, ImmutableArray<NativeBattleModuleDraw> draws)
    {
        if (Slots[slot].GilgameshFlashPresentation is not { } receipt)
        {
            return draws;
        }
        if (draws.Any(draw => draw.Kind != NativeBattleModuleDrawKind.Flash || draw.PilotIdentity != receipt.Identity))
        {
            throw new InvalidDataException("Gilgamesh flash draw differs from its actual allocated source receipt.");
        }
        return draws.Select(draw => draw with { GilgameshFlashPresentation = receipt }).ToImmutableArray();
    }

    internal NativeBattleOriginalEffectState? RecordGilgameshFlashPackets(NativeBattleOriginalEffectState? selected, NativeBattleGilgameshFlashPresentation source, string bank, byte buffer, int start, NativeBattleModulePacketBatch batch)
    {
        if (selected is null || selected.ReturnedDescriptor is null || selected.Binding.Effect != source.Effect || GilgameshPresentation?.Context is not { } context || context.Birth != source.ContextBirth || context.Effect != source.Effect)
        {
            return selected;
        }
        ImmutableArray<NativeBattlePrimitiveReference> references = batch.Primitives.Select(item => new NativeBattlePrimitiveReference(bank, buffer, checked(start + item.ByteOffset))).ToImmutableArray();
        return selected with { PresentationPackets = selected.PresentationPackets.AddRange(references) };
    }

    private void ValidateGilgameshPresentation()
    {
        NativeBattleGilgameshPresentationState? presentation = GilgameshPresentation;
        if (presentation is null)
        {
            if (Slots.Any(task => task.GilgameshFlashPresentation is not null))
            {
                throw new InvalidDataException("Gilgamesh flash provenance has no presentation context owner.");
            }
            return;
        }
        NativeBattleGilgameshPresentationContext context = presentation.Context ?? throw new InvalidDataException("Gilgamesh presentation lost its executed context.");
        if (!OwnsGilgameshPresentation(context.Effect) || context.Birth <= 0 || context.Birth > long.MaxValue - 3 || context.RootGeneration != context.Birth + 1 || context.DirectorGeneration != context.Birth + 2 || !GilgameshSourceAddress(context.DirectorAddress) || context.RootAddress is not (0x21FF468 or 0x21FF478) || presentation.NextBirth <= context.DirectorGeneration)
        {
            throw new InvalidDataException("Gilgamesh presentation context has invalid allocation ordinals or native root identity.");
        }
        HashSet<long> births = [context.Birth, context.RootGeneration, context.DirectorGeneration];
        for (int slot = 0; slot < Slots.Length; slot++)
        {
            NativeBattleModuleTask task = Slots[slot];
            if (task.GilgameshFlashPresentation is not { } receipt)
            {
                continue;
            }
            if (!OwnsGilgameshPresentation(receipt.Effect) || receipt.Slot != slot || receipt.ContextBirth <= 0 || receipt.ContextBirth >= receipt.Birth || receipt.Birth >= presentation.NextBirth || !births.Add(receipt.Birth) || receipt.ContextBirth > context.Birth || receipt.ContextBirth == context.Birth && (receipt.Effect != context.Effect || receipt.SourceBirth != context.DirectorGeneration || receipt.Source?.TaskAddress != context.DirectorAddress) || receipt.ContextBirth > long.MaxValue - 3 || receipt.SourceBirth != receipt.ContextBirth + 2 || receipt.SourceBirth >= receipt.Birth || receipt.ContextBirth < context.Birth && receipt.Birth >= context.Birth || receipt.Source is not { Callback: 0x592300 } source || !GilgameshSourceAddress(source.TaskAddress) || receipt.Counter != 279 || task.Kind != NativeBattleModuleKind.CriticalFlash571300 || task.Callback is not null || task.Payload.Length != 24 || BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan().Slice(2)) != 0xC22 || BinaryPrimitives.ReadInt16LittleEndian(task.Payload.AsSpan().Slice(16)) != 255 || receipt.Identity.Effect != receipt.Effect || receipt.Identity.Bank != DescriptorAddress || receipt.Identity.Slot != slot || receipt.Identity.Generation != receipt.Birth || receipt.Identity.Primitive != unchecked((int)source.Callback) || receipt.Identity.ResourceGeneration != GilgameshFlashResource(receipt.ContextBirth, receipt.SourceBirth, source, receipt.Counter))
            {
                throw new InvalidDataException("Gilgamesh flash provenance differs from its actual source, slot, birth or native flash family.");
            }
        }
    }

    private static long GilgameshFlashResource(long contextBirth, long sourceBirth, NativeBattleGilgameshFlashSource source, int counter) => unchecked(((contextBirth * 397 ^ sourceBirth) * 397 ^ source.TaskAddress) * 397 ^ counter);

    private static bool GilgameshSourceAddress(uint address)
    {
        uint offset = unchecked(address - 0x21FFD68);
        return offset < 100 * 36 && offset % 36 == 0;
    }
}
