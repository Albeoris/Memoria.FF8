using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverNativePhoenixState(int HostSlot, NativeBattleLifeFrame Frame, NativeBattleActionPacketsState Packets, NativeCrtRandomState Random, NativeBattleLogicalOrdering Ordering, bool ContactPublished)
{
    public bool Complete => Frame.Effect.ReturnedDescriptor == 0;
}
internal sealed record CrossoverNativePhoenixStep(CrossoverNativePhoenixState State, bool Contact, ImmutableArray<NativeBattleModuleSound> Sounds);

/// <summary>Original Phoenix Down's distinct Life-family entry and one host ally contact.</summary>
internal sealed class CrossoverNativePhoenixProgram(GameAssets game, NativeBattleGraphicsResource sourceResource)
{
    public CrossoverNativePhoenixState Begin(int hostSlot, NativeBattleContactHalf contact, NativeCrtRandomState random, NativeBattleEffectTextureState textures)
    {
        if (hostSlot is < 0 or >= 3 || contact.ActorIndex != 3 || textures.Count != 0)
        {
            throw new InvalidDataException("Mixed Phoenix Down requires one bound ally and completed casting uploads.");
        }
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[640], new byte[1176]);
        (packets, _) = packets.Prepare(new(0, 4, 15, 0, 7, 5, null));
        (packets, _) = packets.AppendContact(new(contact, new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(5, game.ReadNativeEffectCatalogForEffects([5]));
        selected = selected with { Parameters = new(0, 0, 8, 0, 0, 0) };
        (NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect) = NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), selected);
        NativeBattleLifeResources resources = effect.Binding.LifeResources ?? throw new InvalidDataException("Mixed Phoenix Down lacks its original resources.");
        memory = memory with { Textures = textures with { Slots = memory.Textures!.Slots, Count = memory.Textures.Count } };
        NativeBattleLifeFrame frame = NativeBattleLifeController.Initialize8649C0(memory, effect, packets, resources);
        frame = frame with { Memory = frame.Memory with { Textures = frame.Memory.Textures!.Drain505D20(frame.Memory.Arena!).State } };
        return new(hostSlot, frame, packets, random, NativeBattleLogicalOrdering.Clear(), false);
    }

    public CrossoverNativePhoenixStep Advance(CrossoverNativePhoenixState source, NativeBattleGraphicsModel caster, CrossoverNativeCureForeignTarget target, NativeBattleCameraProjection projection)
    {
        if (source.Complete)
        {
            return new(source, false, []);
        }
        if (target.HostSlot != source.HostSlot)
        {
            throw new InvalidDataException("Mixed Phoenix Down lost its bound host ally.");
        }
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        NativeBattleLifeResources resources = source.Frame.Effect.Binding.LifeResources!;
        NativeBattleLifeInputs inputs = new(source.Packets, projection, ReadModel, index => index == 0 ? sourceResource.PoseSource! : throw new InvalidDataException("Mixed Phoenix Down queried an unbound source pose."), source.Random, Execute) { ReadForeignTarget = index => index == 3 ? target.Anchors : null };
        NativeBattleLifeStep step = NativeBattleLifeController.Advance864AB0(source.Frame, resources, inputs);
        if (step.Contacts.Length > 1 || step.Contacts.Any(index => index != 0) || source.ContactPublished && !step.Contacts.IsEmpty)
        {
            throw new InvalidDataException("Mixed Phoenix Down emitted a repeated or foreign contact.");
        }
        bool contact = !step.Contacts.IsEmpty;
        return new(source with { Frame = step.Frame, Random = step.Random, Ordering = ordering, ContactPublished = source.ContactPublished || contact }, contact, step.Sounds);

        NativeBattleGraphicsModel ReadModel(int index)
        {
            if (index == 0)
            {
                return caster;
            }
            return index == 3 ? new(index, 2) { Transform = target.Transform } : new(index, 0);
        }

        NativeBattleLifeFrame Execute(NativeBattleLifeFrame current, NativeBattleModulePacketBatch batch)
        {
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, current.State.DrawCursor ?? throw new InvalidDataException("Mixed Phoenix Down lost its native packet cursor."), ordering, batch, NativeBattleEffectGouraudWrites.RgbBytes);
            ordering = written.Ordering;
            return current with { Memory = written.Memory, State = current.State with { DrawCursor = written.Cursor } };
        }
    }

    public static CrossoverNativePhoenixState Capture(CrossoverNativePhoenixState source) => source with { Frame = NativeBattleEffectComponentStorage.Capture(source.Frame), Ordering = NativeBattleEffectComponentStorage.Capture(source.Ordering) };
    public static CrossoverNativePhoenixState Restore(CrossoverNativePhoenixState source) => source with { Frame = NativeBattleEffectComponentStorage.Restore(source.Frame), Ordering = NativeBattleEffectComponentStorage.Restore(source.Ordering) };
}
