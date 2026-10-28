using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeBio22_89A6B0(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Bio entry requires its selected effect.");
        NativeBattleBio22Resources resources = effect.Binding.Bio22Resources ?? throw new NotSupportedException("Native Bio requires its authenticated resources.");
        NativeBattleBio22Frame initialized = NativeBattleBio22Controller.Initialize89A6B0(NativeEffectMemory ?? throw new NotSupportedException("Native Bio requires current process memory."), effect, nativeActionPackets, resources);
        NativeEffectMemory = initialized.Memory;
        if ((effect.Parameters!.Flags79 & 1) == 0)
        {
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Bio requires its current camera.")).RegisterControlResource(initialized.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = initialized.Effect with { Bio22 = initialized.State } };
    }

    private NativeBattleGraphicsState AdvanceNativeBio22_89A7B0(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Bio callback requires its selected effect.");
        NativeBattleBio22Resources resources = effect.Binding.Bio22Resources ?? throw new NotSupportedException("Native Bio requires its original resources.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Bio requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Bio requires its published projection.");
        NativeBattleBio22Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Bio requires retained process memory."), effect, effect.Bio22 ?? throw new NotSupportedException("Native Bio requires its executed entry state."));
        short zsf3 = frame.Memory.ScriptedGraphics?.Zsf3 ?? throw new NotSupportedException("Native Bio requires its initialized current ZSF3.");
        NativeBattleBio22Inputs inputs = new(nativeActionPackets, projection, camera.Roll, zsf3, index => graphics.Models[index], index => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Bio requires its actual body pose source."), _ => NativeStage ?? throw new NotSupportedException("Native Bio requires its four stage groups."), ReadProxyDepth, Execute, FinalActor);
        NativeBattleBio22ChildStep step = NativeBattleBio22Controller.Advance89A7B0(frame, resources, inputs, nativeCrtRandom ?? throw new NotSupportedException("Native Bio requires its retained CRT state."));
        NativeEffectMemory = step.Frame.Memory;
        nativeCrtRandom = step.Random;
        return graphics with { OriginalEffect = step.Frame.Effect with { Bio22 = step.Frame.State } };

        ImmutableArray<int> ReadProxyDepth(NativeBattleBio22Frame current, int ordinal)
        {
            NativeBattleLogicalOrdering ordering = (graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Bio requires its current ordering table.")).Pending;
            return ordering.ReadRetainedProxyDepth(ordinal);
        }

        NativeBattleBio22Frame Execute(NativeBattleBio22Frame current, NativeBattleBio22Operation operation)
        {
            NativeEffectMemory = current.Memory;
            switch (operation)
            {
                case NativeBattleBio22Stage stage:
                    NativeStage = stage.Stage;
                    return current;
                case NativeBattleBio22Sound sound:
                    PublishNativeModuleSound(new(sound.Sound, 0, null) { Pan = 128 });
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleBio22Contact contact:
                    var pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory! };
            }
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Bio requires its initialized ordering table.");
            int cursor = current.State.DrawCursor ?? throw new NotSupportedException("Native Bio requires its executed private packet cursor.");
            NativeBattleEffectPacketWrite written = operation switch
            {
                NativeBattleBio22Packets packets => NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, packets.Batch),
                NativeBattleBio22Sprite sprite => NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, sprite.Batch, NativeBattleEffectGouraudWrites.RgbBytes),
                _ => throw new NotSupportedException("Native Bio operation has no original service.")
            };
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            NativeEffectMemory = written.Memory;
            return current with { Memory = written.Memory, State = current.State with { DrawCursor = written.Cursor } };
        }

        NativeBattleBio22ChildStep FinalActor(NativeBattleBio22Frame current, uint address, NativeCrtRandomState random)
        {
            NativeBattleEffectHierarchy hierarchy = new(current.Effect.Pools);
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            uint rootAddress = task.ReadDword(0x10);
            NativeBattleBio22ActorFrame actor = new(task, hierarchy.Read(rootAddress), current.Memory, graphics.Models);
            NativeBattleBio22ActorServices services = new(ReadLocal, Shadow, Body);
            NativeBattleBio22ActorStep changed = NativeBattleBio22Actor.Advance89D3D0(actor, actual => NativeBattleBio22Actor.Draw89D460(actual, projection.View ?? throw new NotSupportedException("Native Bio actor requires its actual view matrix."), services));
            graphics = graphics with { Models = changed.Frame.Models };
            hierarchy = hierarchy.Write(rootAddress, changed.Frame.Root).Write(address, changed.Frame.Task);
            return new(current with { Memory = changed.Frame.Memory, Effect = current.Effect with { Pools = hierarchy.Pools } }, random, unchecked((int)changed.Result));

            NativeBattleBio22ActorFrame ReadLocal(NativeBattleBio22ActorFrame actual, int index)
            {
                graphics = graphics with { Models = actual.Models };
                NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Bio ReadLocal requires the actual body source.");
                NativeBattleGraphicsModel published = NativeBattleBodyBoneMatrices.Publish508C90(actual.Models[index], source);
                return actual with { Models = actual.Models.SetItem(index, published) };
            }

            NativeBattleBio22ActorFrame Shadow(NativeBattleBio22ActorFrame actual, int index, NativeBattleActionMatrix main, int bucket, int shift)
            {
                if (shift != 16)
                {
                    throw new NotSupportedException("Native Bio shadow requires its original depth shift.");
                }
                graphics = graphics with { Models = actual.Models };
                NativeEffectMemory = actual.Memory;
                graphics = SubmitNativeShadowPackets5088A0(graphics, actual.Models[index], projection, bucket, false);
                return actual with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Bio shadow lost process memory.") };
            }

            NativeBattleBio22ActorFrame Body(NativeBattleBio22ActorFrame actual, int index, NativeBattleActionMatrix main, short phase, short amplitude)
            {
                graphics = graphics with { Models = actual.Models };
                NativeBattleGraphicsModel model = actual.Models[index];
                NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
                NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Native Bio body requires its actual skeleton.");
                ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(model, source);
                NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native Bio body requires its current mesh mask.");
                short zsf4 = actual.Memory.ScriptedGraphics?.Zsf4 ?? throw new NotSupportedException("Native Bio body requires its initialized current ZSF4.");
                foreach (NativeBattleCureActorGeometry geometry in NativeBattleCureActorAssembly.SelectGeometry8D7110(model, resource, sequence.MeshMask124))
                {
                    NativeBattleBio22ActorMemory owner = actual.Memory.Bio22ActorMemory ?? throw new NotSupportedException("Native Bio body requires its current physical projected work.");
                    NativeBattleBio22ActorDraw drawn = NativeBattleBio22ActorPackets.Emit89D780(geometry.Mesh, bones, main, projection, model.Color40 ?? throw new NotSupportedException("Native Bio body requires its actual color."), phase, amplitude, zsf3, zsf4, owner.Projected, 0, resource.GeometryRelocated507550 ? null : geometry.Binding);
                    owner = owner with { Projected = drawn.Work };
                    owner.Validate();
                    actual = actual with { Memory = actual.Memory with { Bio22ActorMemory = owner } };
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Bio body requires its initialized ordering table.");
                    NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(actual.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Bio body requires its actual COMMON cursor."), primitive.Pending, drawn.Packets);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    actual = actual with { Memory = actual.Memory with { PrimitiveProcess = written.Process } };
                }
                return actual;
            }
        }
    }
}
