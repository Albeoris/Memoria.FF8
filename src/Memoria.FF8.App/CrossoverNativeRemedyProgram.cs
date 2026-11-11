using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverNativeRemedyState(int? ForeignSlot, NativeBattleRestorationFrame Frame, NativeBattleActionPacketsState Packets, NativeCrtRandomState Random, NativeBattleLogicalOrdering Ordering, short StageDepth, bool ContactPublished)
{
    public bool Complete => Frame.Effect.ReturnedDescriptor == 0;
}
internal sealed record CrossoverNativeRemedyStep(CrossoverNativeRemedyState State, bool Contact, ImmutableArray<NativeBattleModuleSound> Sounds);

/// <summary>Original Remedy lifecycle under the host camera, with origin-owned packet and random state.</summary>
internal sealed class CrossoverNativeRemedyProgram(GameAssets game, NativeBattleGraphicsResource sourceResource)
{
    public CrossoverNativeRemedyState Begin(int? foreignSlot, NativeBattleContactHalf contact, NativeCrtRandomState random, NativeBattleEffectTextureState textures)
    {
        if (foreignSlot is < 0 or >= 3 || contact.ActorIndex != (foreignSlot is null ? 0 : 3) || textures.Count != 0)
        {
            throw new InvalidDataException("Mixed Remedy requires one bound party contact and completed casting uploads.");
        }
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[640], new byte[1176]);
        (packets, _) = packets.Prepare(new(0, 4, 15, 0, 16, 67, null));
        (packets, _) = packets.AppendContact(new(contact, new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(67, game.ReadNativeEffectCatalogForEffects([67]));
        selected = selected with { Parameters = new(0, 0, (ushort)(1 << contact.ActorIndex), 0, 0, 0) };
        (NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect) = NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), selected);
        NativeBattleRestorationExecutableState executable = game.ReadColdProcessNativeBattleMemory().EffectMemory?.RemedyExecutable ?? throw new InvalidDataException("Mixed Remedy lacks its original initialized loader memory.");
        NativeBattleRestorationResources resources = effect.Binding.RestorationResources ?? throw new InvalidDataException("Mixed Remedy lacks its original resources.");
        memory = memory with { RemedyExecutable = executable, Textures = textures with { Slots = memory.Textures!.Slots, Count = memory.Textures.Count } };
        NativeBattleRestorationFrame frame = NativeBattleRestorationController.Initialize(memory, effect, packets, resources);
        frame = frame with { Memory = frame.Memory with { Textures = frame.Memory.Textures!.Drain505D20(frame.Memory.Arena!).State } };
        return new(foreignSlot, frame, packets, random, NativeBattleLogicalOrdering.Clear(), 0, false);
    }

    public CrossoverNativeRemedyStep Advance(CrossoverNativeRemedyState source, NativeBattleGraphicsModel caster, CrossoverNativeCureForeignTarget? target, NativeBattleCameraProjection projection)
    {
        if (source.Complete)
        {
            return new(source, false, []);
        }
        if (source.ForeignSlot != target?.HostSlot)
        {
            throw new InvalidDataException("Mixed Remedy lost its bound recipient.");
        }
        NativeBattlePoseSource pose = sourceResource.PoseSource!;
        caster = NativeBattlePoseMatrices.PublishShadowBounds5086F0(caster, pose);
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        NativeCrtRandomState random = source.Random;
        short depth = source.StageDepth;
        NativeBattleRestorationResources resources = source.Frame.Effect.Binding.RestorationResources!;
        NativeBattleRestorationProfile profile = NativeBattleRestorationProfile.ForEffect(resources.Effect);
        NativeBattleRestorationControllerInputs inputs = new(source.Packets, projection.View!, ReadModel, index => index == 0 ? pose : throw new InvalidDataException("Mixed Remedy queried an unbound source pose.")) { ReadForeignTarget = index => index == 3 ? target?.Anchors : null };
        NativeBattleRestorationControllerStep step = NativeBattleRestorationController.Advance(source.Frame, inputs, resources, (frame, address) =>
        {
            NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
            return task.Callback == profile.GenericCallback
                ? NativeBattleRestorationGeneric.Advance(frame, address, new((current, context) => NativeBattleRestorationContext.Initialize(current, context, resources, inputs), (current, context) => NativeBattleRestorationNodeEngine.Advance(current, context, AdvanceNodes)))
                : NativeBattleRestorationFade.Advance(frame, address, ApplyFade);
        });
        if (step.Contacts.Length > 1 || step.Contacts.Any(index => index != 0) || source.ContactPublished && !step.Contacts.IsEmpty)
        {
            throw new InvalidDataException("Mixed Remedy emitted a repeated or foreign contact.");
        }
        bool contact = !step.Contacts.IsEmpty;
        return new(source with { Frame = step.Frame, Random = random, Ordering = ordering, StageDepth = depth, ContactPublished = source.ContactPublished || contact }, contact, step.Sounds);

        NativeBattleGraphicsModel ReadModel(int index)
        {
            if (index == 0)
            {
                return caster;
            }
            return index == 3 && target is not null ? new(index, 2) { Transform = target.Transform, GeometryBounds52 = target.Anchors.LocalBounds } : new(index, 0);
        }

        NativeBattleRestorationFrame AdvanceNodes(NativeBattleRestorationFrame frame, uint context)
        {
            NativeBattleRestorationPresentationStep nodes = NativeBattleRestorationLifecycle.AdvanceAndDraw(frame, context, random, ReadModel, projection, 0, ordering, game.ReadNativeBattleGraphicsRules().SummonColors);
            random = nodes.Random;
            ordering = nodes.Ordering;
            return nodes.Frame;
        }

        NativeBattleRestorationFrame ApplyFade(NativeBattleRestorationFrame frame, NativeBattleStageFadeWrite write)
        {
            depth = write.Fade;
            return frame;
        }
    }

    public static CrossoverNativeRemedyState Capture(CrossoverNativeRemedyState source) => source with { Frame = NativeBattleEffectComponentStorage.Capture(source.Frame), Ordering = NativeBattleEffectComponentStorage.Capture(source.Ordering) };
    public static CrossoverNativeRemedyState Restore(CrossoverNativeRemedyState source) => source with { Frame = NativeBattleEffectComponentStorage.Restore(source.Frame), Ordering = NativeBattleEffectComponentStorage.Restore(source.Ordering) };
}
