using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public NativeBattleGraphicsState? NativeGraphics { get; private set; }
    public bool NativeEntryReady => NativeGraphics?.EntryReady ?? true;
    public bool? NativeGraphicsLoading => NativeGraphics is null ? null : NativeGraphics.ModelLoadCount != 0;
    public byte NativeEffectTaskCount => (NativeGraphics ?? throw new NotSupportedException("Native graphics completion requires its original effect task owner.")).Effects.PublishedCount;

    private void RequireNativeEntryReady()
    {
        if (!NativeEntryReady)
        {
            throw new InvalidOperationException("Native battle commands wait for the original entry112 completion callback.");
        }
    }

    private void InitializeNativeGraphics()
    {
        if (definition.NativeGraphics is not { } source)
        {
            return;
        }
        ValidateNativeGraphicsDefinition(source);
        if (NativeCamera is not null)
        {
            NativeCamera = NativeCamera.Initialize500400();
        }
        NativeStage = NativeStage?.Reset506C50();
        if (NativeEffectMemory is { } effectMemory)
        {
            //500870 calls501160: DB8 is reset, while the pending DB4 field is retained.
            NativeEffectMemory = effectMemory with { SoundPublishedFlags = 0, Textures = effectMemory.Textures?.Initialize500400() };
        }
        InitializeNativePartyPresentation();
        NativeModelTextureCache = NativeBattleModelTextureCache.Initialize508260(NativeModelTextureCache);
        NativeBattleGraphicsState graphics = new(NativeBattleGraphicsModels.ClearBank().ToImmutableArray(), Enumerable.Repeat<string?>(null, 7).ToImmutableArray(), 0, 0);
        graphics = InitializeNativePrimitiveFrame500400(graphics);
        NativeGraphics = QueueNativeEntryLoads(graphics);
    }

    private static byte NativeStandingSequence(uint flags)
    {
        return (flags & 2) != 0 ? (byte)3 : (flags & 0x1000) != 0 ? (byte)2 : (flags & 0x1000000) != 0 ? (byte)16 : (flags & 0x800000) != 0 ? (byte)19 : (flags & 0x200000) != 0 ? (byte)29 : (flags & 0x41021) != 0 ? (byte)2 : (byte)1;
    }

    private void QueueNativeContactPacket(NativeBattleContactPacket packet)
    {
        packet.Validate();
        NativeBattleGraphicsState graphics = NativeGraphics ?? throw new NotSupportedException("Native contact presentation requires its graphical descriptor owner.");
        NativeGraphics = graphics with { PendingContacts = graphics.PendingContacts.Add(packet) };
    }

    private void RestoreNativeGraphics(NativeBattleGraphicsState? state)
    {
        if ((state is null) != (definition.NativeGraphics is null))
        {
            throw new InvalidDataException("Native graphics owner is missing; migrate the standalone snapshot explicitly.");
        }
        state?.Validate();
        if (state is not null)
        {
            ValidateNativeTriggerWidget(state);
            for (int index = 0; index < 7; index++)
            {
                if (state.ActorIds[index] is string id && (!byId.ContainsKey(id) || FindNativeGraphicsBinding(id) is null))
                {
                    throw new InvalidDataException("Native graphics row does not identify its actual allocation and immutable resource.");
                }
                NativeBattleGraphicsModel model = state.Models[index];
                if ((model.Flags0 & 2) != 0 && model.TextureResourceIndex is int resourceIndex)
                {
                    NativeBattleModelTextureCache cache = NativeModelTextureCache ?? throw new InvalidDataException("Saved native graphics resource index has no retained texture cache owner.");
                    NativeBattleModelTextureCacheRow row = cache.Rows[resourceIndex];
                    if (row.Kind == 0 || row.ModelId != model.ModelId4 || row.Binding is not { } binding || model.BodyTextureSlots is not { } material || !binding.PaletteRows.SequenceEqual(material.PaletteRows))
                    {
                        throw new InvalidDataException("Saved native graphics material differs from its physical texture resource row.");
                    }
                }
            }
        }
        NativeGraphics = state;
    }

    private NativeBattleGraphicsBinding? FindNativeGraphicsBinding(string actorId)
    {
        if (definition.NativeGraphics is not { } graphics || !byId.TryGetValue(actorId, out BattleCombatant? actor))
        {
            return null;
        }
        NativeBattleGraphicsBinding? direct = graphics.Bindings.SingleOrDefault(binding => binding.ActorId == actorId);
        if (direct is not null || actor.SceneSlot is not int sceneSlot)
        {
            return direct;
        }
        //Every dynamic allocation retains its immutable original scene resource; the graphics row may outlive the battle-slot assignment.
        string prototypeId = definition.Combatants.Single(prototype => !prototype.IsPlayer && prototype.SceneSlot == sceneSlot).Id;
        return graphics.Bindings.SingleOrDefault(binding => binding.ActorId == prototypeId);
    }

    private void ValidateNativeGraphicsDefinition(NativeBattleGraphicsDefinition source)
    {
        if (source.Rules is null || source.Bindings.IsDefault || source.InitialLoadOrder.IsDefault || source.Bindings.Any(binding => binding is null || binding.Resource is null || !byId.ContainsKey(binding.ActorId)) || source.Bindings.Select(binding => binding.ActorId).Distinct(StringComparer.Ordinal).Count() != source.Bindings.Length || source.InitialLoadOrder.Distinct(StringComparer.Ordinal).Count() != source.InitialLoadOrder.Length)
        {
            throw new InvalidDataException("Native graphics definition lacks its original actor bindings and initialization order.");
        }
        source.Rules.Validate();
        foreach (NativeBattleGraphicsBinding binding in source.Bindings)
        {
            binding.Resource.Validate();
            if (binding.Resource.AnimationFrames.IsDefaultOrEmpty)
            {
                throw new InvalidDataException("Native graphics binding lacks original animation frame counts.");
            }
        }
        if (source.InitialLoadOrder.Any(id => !source.Bindings.Any(binding => binding.ActorId == id)))
        {
            throw new InvalidDataException("Native graphics initialization order names an absent resource binding.");
        }
        if (!source.InitialLoadOrder.ToHashSet(StringComparer.Ordinal).SetEquals(combatants.Where(actor => actor.NativeIndex is >= 0 and < 7).Select(actor => actor.Id)))
        {
            throw new InvalidDataException("Native graphics load order omits an allocated physical participant.");
        }
    }
}
