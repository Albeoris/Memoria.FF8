using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using Memoria.FF8.Rendering;

namespace Memoria.FF8;

internal sealed record CrossoverNativeCureState(NativeBattleCureFrame Frame, NativeBattleActionPacketsState Packets, NativeBattleGraphicsModel Model, NativeCrtRandomState Random, NativeBattleLogicalOrdering Ordering, bool ContactPublished)
{
    public int? ForeignSlot { get; init; }
    public bool ForeignHidden { get; init; }
    public bool ForeignBodyDrawn { get; init; }
    public bool Complete => Frame.Effect.ReturnedDescriptor == 0;
}
internal sealed record CrossoverNativeCureStep(CrossoverNativeCureState State, bool Contact, ImmutableArray<NativeBattleModuleSound> Sounds);

/// <summary>Original Cure controller with source or bound host ally geometry in one host callback.</summary>
internal sealed class CrossoverNativeCureProgram(GameAssets game, NativeBattleGraphicsResource resource)
{
    private readonly NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();

    public CrossoverNativeCureState Begin(NativeBattleGraphicsModel model, NativeBattleContactHalf contact, NativeCrtRandomState random, NativeBattleEffectTextureState? sharedTextures = null, int? foreignSlot = null, ushort effectId = 1)
    {
        if (effectId is not (1 or 50))
        {
            throw new NotSupportedException("Mixed Cure-family recovery requires its original Cure or Potion module.");
        }
        model = model with { Color40 = model.Color40 ?? rules.SummonColors!.Transition, Color44 = model.Color44 ?? rules.SummonColors!.Normal };
        if (model is not { Index: 0, ModelId4: >= 0 and <= 10, Color40: not null, Color44: not null } || (model.Flags0 & 2) == 0 || (model.Flags2!.Value & 4) != 0 || foreignSlot is < 0 or >= 3 || contact.ActorIndex != (foreignSlot is null ? 0 : 3))
        {
            throw new InvalidDataException("Mixed Cure requires its loaded original caster and one prepared party contact.");
        }
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[640], new byte[1176]);
        (packets, _) = packets.Prepare(effectId == 50 ? new(0, 4, 15, 0, 1, 50, null) : new(0, 2, 11, 0, 21, 1, null));
        (packets, _) = packets.AppendContact(new(contact, new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(effectId, game.ReadNativeEffectCatalogForEffects([effectId]));
        selected = selected with { Parameters = new(0, 0, (ushort)(1 << contact.ActorIndex), 0, 0, 0) };
        (NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect) = NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), selected);
        NativeBattleModelTextureLoad body = NativeBattleModelTextures.Prepare507400(resource.BodyTexturePack ?? throw new InvalidDataException("Mixed Cure requires the original body TIM pack."), 0);
        NativeBattleEffectTextureState textures = NativeBattleModelTextures.Enqueue507400(memory.Textures!, body);
        if (sharedTextures is not null)
        {
            if (sharedTextures.Count != 0)
            {
                throw new InvalidDataException("Cure requires completed source texture uploads before effect entry.");
            }
            textures = sharedTextures with { Slots = memory.Textures!.Slots, Count = memory.Textures.Count };
        }
        NativeBattleModelTextureBinding? linked = null;
        if (resource.LinkedGeometry is { IsEmpty: false })
        {
            NativeBattleModelTextureLoad weapon = NativeBattleModelTextures.Prepare507400(resource.LinkedTexturePack ?? throw new InvalidDataException("Mixed Cure requires the original linked weapon TIM pack."), body.AllocatedSlots);
            if (sharedTextures is null)
            {
                textures = NativeBattleModelTextures.Enqueue507400(textures, weapon);
            }
            linked = weapon.Binding;
        }
        model = NativeBattlePoseMatrices.PublishShadowBounds5086F0(model with { BodyTextureSlots = body.Binding, LinkedTextureSlots = linked, TextureAllocationMask = body.AllocatedSlots }, resource.PoseSource!);
        memory = memory with { Textures = textures, PrimitiveProcess = memory.PrimitiveProcess!.Initialize500400().Reset500900(0) };
        NativeBattleCureResources resources = effect.Binding.CureResources ?? throw new InvalidDataException("Mixed Cure lacks its original executable resources.");
        NativeBattleCureFrame frame = NativeBattleCureController.Initialize8D6A00(memory, effect, packets, resources);
        frame = frame with { Memory = frame.Memory with { Textures = frame.Memory.Textures!.Drain505D20(frame.Memory.Arena!).State } };
        return new(frame, packets, model, random, NativeBattleLogicalOrdering.Clear(), false) { ForeignSlot = foreignSlot };
    }

    public CrossoverNativeCureStep Advance(CrossoverNativeCureState source, NativeBattleCameraProjection projection, CrossoverNativeCureForeignTarget? foreign = null)
    {
        if (source.Complete)
        {
            return new(source, false, []);
        }
        if (source.ForeignSlot != foreign?.HostSlot)
        {
            throw new InvalidDataException("Mixed Cure continuation lost its bound host ally geometry.");
        }
        byte alias = foreign is null ? (byte)0 : (byte)3;
        bool foreignHidden = source.ForeignHidden;
        bool foreignBodyDrawn = false;
        NativeBattleCureFrame current = source.Frame with { Memory = source.Frame.Memory with { PrimitiveProcess = source.Frame.Memory.PrimitiveProcess!.Reset500900(0) } };
        NativeBattleGraphicsModel model = NativeBattlePoseMatrices.PublishShadowBounds5086F0(source.Model, resource.PoseSource!);
        NativeCrtRandomState random = source.Random;
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        NativeBattleCureResources resources = current.Effect.Binding.CureResources!;
        NativeBattleCureControllerInputs inputs = new(source.Packets, projection.View!, ReadModel, index => index == 0 ? resource.PoseSource! : throw new InvalidDataException("Mixed Cure queried an unbound pose source.")) { ReadForeignTarget = index => index == 3 ? foreign?.Anchors : null };
        NativeBattleCureControllerStep step = NativeBattleCureController.Advance8D6B30(current, inputs, resources, (frame, address) =>
        {
            frame = frame with { Memory = current.Memory };
            NativeBattleCureParticleStep particle = NativeBattleCureParticles.AdvancePrivate(frame, address, resources, new(inputs, random, Execute) { Projection = projection });
            current = particle.Frame;
            random = particle.CrtRandomAfter;
            return new(particle.Frame, particle.Result);
        });
        if (step.Contacts.Length > 1 || step.Contacts.Any(index => index != 0) || source.ContactPublished && !step.Contacts.IsEmpty)
        {
            throw new InvalidDataException("Mixed Cure emitted a repeated or foreign contact pointer.");
        }
        bool contact = !step.Contacts.IsEmpty;
        return new(source with { Frame = step.Frame, Model = model, Random = random, Ordering = ordering, ForeignHidden = foreignHidden, ForeignBodyDrawn = foreignBodyDrawn, ContactPublished = source.ContactPublished || contact }, contact, step.Sounds);

        NativeBattleGraphicsModel ReadModel(int index)
        {
            if (index == 0)
            {
                return model;
            }
            return index == 3 && foreign is not null ? new(index, (ushort)(foreignHidden ? 6 : 2)) { Transform = foreign.Transform } : new(index, 0);
        }

        NativeBattleCureFrame Execute(NativeBattleCureFrame frame, NativeBattleCureOperation operation)
        {
            current = frame;
            switch (operation)
            {
                case NativeBattleCureActorInitializeOperation initialize when initialize.Target == alias:
                    current = current with { State = current.State with { ActorContext = new(alias) } };
                    break;
                case NativeBattleCureActorVisibilityOperation visibility when visibility.Target == alias:
                    if (foreign is not null)
                    {
                        foreignHidden = visibility.Hidden;
                    }
                    else
                    {
                        model = model with { Flags0 = visibility.Hidden ? (ushort)(model.Flags0 | 4) : (ushort)(model.Flags0 & ~4) };
                    }
                    break;
                case NativeBattleCureSpriteOperation sprite:
                    AppendEffect(NativeBattleModulePackets.EmitSprite571DC0(sprite.Draw, projection, 0, 17, 2));
                    break;
                case NativeBattleCurePacketOperation packets:
                    AppendEffect(packets.Batch);
                    break;
                case NativeBattleCureActorDrawOperation actor when actor.Target == alias:
                    if (foreign is not null)
                    {
                        foreignBodyDrawn = true;
                        AppendForeignOverlay(foreign, actor);
                        break;
                    }
                    AppendShadow();
                    NativeBattleCureActorPacketBatch actorPackets = NativeBattleCureActorAssembly.Emit8D7110(model, resource, current.State.CopiedCamera!, projection, actor.Center, actor.VerticalOffset, ActorContextF4(), actor.PilotIdentity);
                    foreach (NativeBattleCureAllocatedPrimitive packet in actorPackets.Primitives)
                    {
                        if (packet.Bank == NativeBattleCurePacketBank.Process)
                        {
                            AppendCommon(packet.Primitive);
                        }
                        else
                        {
                            AppendEffect(new([new(packet.Primitive, 0, 0)], packet.Primitive.Words.Length * 4));
                        }
                    }
                    break;
                default:
                    throw new NotSupportedException("Mixed Cure requires the actual bound actor operation.");
            }
            return current;
        }

        short? ActorContextF4() => current.Effect.Binding.Effect == 50 ? current.Memory.Potion50ContextF4 : current.Memory.CureContextF4;

        void AppendForeignOverlay(CrossoverNativeCureForeignTarget target, NativeBattleCureActorDrawOperation actor)
        {
            NativeBattleCureActorMatrices matrices = NativeBattlePoseMatrices.CureActorMatrices8D7110(target.World, current.State.CopiedCamera!, new(0, actor.VerticalOffset, 0), 1, actor.Center, 8192);
            ushort? rejectBackfaces = ActorContextF4() is short known ? unchecked((ushort)known) : null;
            foreach (Ff7BattleActorFace face in target.Geometry.Faces)
            {
                NativeBattleGeometryPrimitive? overlay = NativeBattleCureActorPackets.ProjectTriangleOverlay8D7430(target.LocalVertex(face.A), target.LocalVertex(face.B), target.LocalVertex(face.C), matrices.MainCamera, matrices.OverlayCamera, projection, rejectBackfaces, 17);
                if (overlay is not null)
                {
                    AppendEffect(new([new(overlay, 0, 0)], overlay.Words.Length * 4));
                }
            }
        }

        void AppendEffect(NativeBattleModulePacketBatch batch)
        {
            NativeBattleCurePacketWrite written = NativeBattleCurePacketArena.AppendPrimary(current.Memory, current.State, ordering, batch);
            current = current with { Memory = written.Memory, State = written.State };
            ordering = written.Ordering;
        }

        void AppendShadow()
        {
            if ((model.Flags0 & 0x20) != 0)
            {
                return;
            }
            NativeBattleShadowGeometry shape = NativeBattlePoseMatrices.ShadowGeometry5088A0(model, model.GeometryBounds52!, projection.View!, rules.ShadowSource!, true);
            NativeBattleModuleProjectedVertex[] vertices = shape.Vertices.Select(vertex => NativeBattleModuleProjection.Project45FE10(shape.CameraMatrix, vertex, projection)).ToArray();
            NativeBattleGfGeometryBatch batch = NativeBattleShadowPackets.EmitBatch5088A0(vertices, model.Color44!.Value, true, 4112, 16);
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess!, ordering, batch);
            current = current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
            ordering = written.Ordering;
        }

        void AppendCommon(NativeBattleGeometryPrimitive primitive)
        {
            NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess!;
            int cursor = process.ByteCursor!.Value;
            ordering = ordering.Prepend(primitive.Bucket, new(process.PacketBank!, process.PacketBuffer, cursor), primitive.Words, primitive.VertexPayload4.IsEmpty ? (ushort)1 : (ushort)0, primitive.VertexPayload4);
            process = NativeBattleCommonPacketArena.StorePrimitive(process, cursor, primitive, true) with { ByteCursor = checked(cursor + primitive.Words.Length * 4) };
            current = current with { Memory = current.Memory with { PrimitiveProcess = process } };
        }
    }

    public static CrossoverNativeCureState Capture(CrossoverNativeCureState source) => source with { Frame = NativeBattleEffectComponentStorage.Capture(source.Frame), Ordering = NativeBattleEffectComponentStorage.Capture(source.Ordering) };
    public static CrossoverNativeCureState Restore(CrossoverNativeCureState source) => source with { Frame = NativeBattleEffectComponentStorage.Restore(source.Frame), Ordering = NativeBattleEffectComponentStorage.Restore(source.Ordering) };
}
