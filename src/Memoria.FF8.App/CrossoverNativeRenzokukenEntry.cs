using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverNativeRenzokukenEntryState(int HostTarget, NativeBattleRenzokuken160Frame Frame, NativeBattleActionPacketsState Packets, NativeBattleRenzokuken160Resources Resources, int Callbacks);
internal sealed record CrossoverNativeRenzokukenPrelude(CrossoverNativeRenzokukenEntryState State, ImmutableArray<NativeBattleRenzokuken160Operation> Operations);

/// <summary>Original ordinary Renzokuken control packet, resource entry and prelude for a host-owned occurrence.</summary>
internal static class CrossoverNativeRenzokukenEntry
{
    public static CrossoverNativeRenzokukenEntryState Begin(GameAssets game, int hostTarget, ushort variant, bool finisherPending, NativeCrtRandomState random, NativeBattleEffectTextureState? sharedTextures = null)
    {
        if (hostTarget is < 4 or > 9 || variant is not (141 or 160) || sharedTextures is { Count: not 0 })
        {
            throw new InvalidDataException("The guest Renzokuken entry requires its ordinary native variant and one host enemy.");
        }
        NativeBattleRenzokuken160Resources resources = variant == 141 ? game.ReadNativeRenzokuken141Resources() : game.ReadNativeRenzokuken160Resources();
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[640], new byte[1176]);
        (packets, _) = packets.Prepare(new(0, 5, 21, -1, finisherPending ? (ushort)0xFFFE : (ushort)0xFFFD, variant, null));
        //The original command5 control has attack type0 and does not resolve damage before its trigger windows.
        NativeBattleContactPresentation neutral = NativeBattleContactPresentations.Complete(0, 1, 0, 0, 255);
        NativeBattleContactHalf contact = NativeBattleContactHalf.FromScratch(3, neutral.Sequence, neutral.Flags2, neutral.HitFlags3, 0, 0, 0);
        (packets, _) = packets.AppendContact(new(contact, new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        packets = packets.WritePlayerRepeatMode(0, 1);
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(variant, game.ReadNativeEffectCatalogForEffects([variant]));
        selected = selected with { Parameters = new(0, 0, 8, 0, 0, 0) };
        (NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect) = NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), selected);
        if (sharedTextures is not null)
        {
            memory = memory with { Textures = sharedTextures with { Slots = memory.Textures!.Slots, Count = memory.Textures.Count } };
        }
        NativeBattleRenzokuken160Frame frame = variant == 141 ? NativeBattleRenzokukenVariantController.Enter61DE70(memory, effect, packets, resources, random) : NativeBattleRenzokuken160Controller.Enter5FF080(memory, effect, packets, resources, random);
        frame = frame with { Memory = frame.Memory with { Textures = frame.Memory.Textures!.Drain505D20(frame.Memory.Arena!).State } };
        return new(hostTarget, frame, packets, resources, 0);
    }

    public static CrossoverNativeRenzokukenPrelude AdvancePrelude(CrossoverNativeRenzokukenEntryState source, NativeBattleGraphicsModel caster)
    {
        if (source.Callbacks is < 0 or >= 2 || caster is not { Index: 0, ModelId4: 0 } || (caster.Flags0 & 2) == 0)
        {
            throw new InvalidOperationException("The source prelude requires its loaded Squall and one of the two original entry callbacks.");
        }
        ImmutableArray<NativeBattleRenzokuken160Operation>.Builder operations = ImmutableArray.CreateBuilder<NativeBattleRenzokuken160Operation>();
        NativeBattleRenzokuken160Inputs inputs = new(0, [], actor => actor == 0 ? caster : throw new InvalidDataException("The native prelude queried an unbound actor."), Execute);
        bool firstVariant = source.Resources.ActorResource.MagicId == 141;
        NativeBattleRenzokuken160Frame frame = firstVariant
            ? NativeBattleRenzokukenVariantController.Advance61F710(source.Frame, (current, pool, slot) => NativeBattleRenzokukenVariantController.AdvanceController(current, pool, slot, inputs, source.Resources, NativeBattleRenzokukenVariantProfile.Renzokuken141), BeforeParticles)
            : NativeBattleRenzokuken160Controller.Advance600930(source.Frame, (current, pool, slot) => NativeBattleRenzokuken160Controller.Advance5FF180(current, pool, slot, inputs, source.Resources), BeforeParticles);
        return new(source with { Frame = frame, Callbacks = source.Callbacks + 1 }, operations.ToImmutable());

        NativeBattleRenzokuken160Frame Execute(NativeBattleRenzokuken160Frame frame, NativeBattleRenzokuken160Operation operation)
        {
            if (operation is not (NativeBattleRenzokuken160ActorFlags or NativeBattleRenzokuken160ActorResource or NativeBattleRenzokuken160Stage or NativeBattleRenzokuken160ActorColor or NativeBattleRenzokuken160Sound))
            {
                throw new InvalidDataException("The original Renzokuken prelude emitted an out-of-phase operation.");
            }
            operations.Add(operation);
            return frame;
        }

        static NativeBattleRenzokuken160Frame BeforeParticles(NativeBattleRenzokuken160Frame frame) => frame;
    }

    public static (NativeBattleGraphicsModel Model, NativeBattleGraphicsResource Resource) ReplaceSource(NativeBattleGraphicsModel model, NativeBattleGraphicsResource previous, NativeBattleRenzokuken160ActorResource binding)
    {
        NativeBattleGraphicsResource replacement = binding.Resource.BindGraphicsResource(previous.PoseSource ?? throw new InvalidDataException("The Renzokuken source has no original body skeleton."), previous.LinkedPoseSource);
        model = NativeBattleSequenceSelections.RetainDecodedPose5022C0(model, previous);
        model = model with { ResourceMagicId = binding.Resource.MagicId, Flags2 = (ushort)((model.Flags2 ?? throw new InvalidDataException("The source resource change has no model WORD2.")) | 8) };
        if (((model.Flags8 ?? throw new InvalidDataException("The source resource change has no model DWORD8.")) & 0x18) == 0)
        {
            model = NativeBattleSequenceSelections.SelectResource505BC0(model, replacement, binding.Sequence);
        }
        return (model, replacement);
    }

    public static CrossoverNativeRenzokukenEntryState Capture(CrossoverNativeRenzokukenEntryState source) => source with { Frame = NativeBattleEffectComponentStorage.Capture(source.Frame) };
    public static CrossoverNativeRenzokukenEntryState Restore(CrossoverNativeRenzokukenEntryState source) => source with { Frame = NativeBattleEffectComponentStorage.Restore(source.Frame) };
}
