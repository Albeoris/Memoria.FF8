using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeCuraga87DB10(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native87DB10 requires its selected original effect.");
        NativeBattleCuragaResources resources = effect.Binding.CuragaResources ?? throw new NotSupportedException("Native87DB10 requires its authenticated executable resources.");
        NativeBattleCuragaFrame frame = NativeBattleCuragaController.Initialize87DB10(NativeEffectMemory ?? throw new NotSupportedException("Native87DB10 requires retained process memory."), effect, nativeActionPackets, resources);
        NativeEffectMemory = frame.Memory;
        if ((effect.Parameters!.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Native87DB10 requires its5099A0 animation registration.");
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native87DB10 requires the original camera owner.")).RegisterControlResource(textures.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect with { Curaga = frame.State } };
    }

    private NativeBattleGraphicsState AdvanceNativeCuraga87DC50(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native87DC50 requires its original effect owner.");
        NativeBattleCuragaResources resources = effect.Binding.CuragaResources ?? throw new NotSupportedException("Native87DC50 requires its authenticated resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native87DC50 requires its prior projection registers.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native87DC50 requires the previously published1D97778 matrix.");
        NativeBattleCuragaFrame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native87DC50 requires retained effect memory."), effect, effect.Curaga ?? throw new NotSupportedException("Native87DC50 requires its executed87DB10 state."));
        NativeBattleCuragaControllerInputs inputs = new(nativeActionPackets, view, index => graphics.Models[index], index => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Curaga anchors require the original model pose source.")) { ApplyContact = ApplyContact };
        NativeBattleCuragaControllerStep step = NativeBattleCuragaController.Advance87DC50(frame, inputs, resources, (current, address) =>
        {
            NativeEffectMemory = current.Memory;
            NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
            NativeBattleCuragaLeafStep leaf = task.Callback == NativeBattleCuragaFamilyProfile.ForEffect(effect.Binding.Effect).Callback(0x87E1A0)
                ? NativeBattleCuragaNodeEngine.AdvanceGeneric87E1A0(current, address, resources, inputs, new(AdvanceNodes))
                : NativeBattleCuragaParticles.AdvanceSimple(current, address, resources, Execute);
            NativeEffectMemory = leaf.Frame.Memory;
            return leaf;
        });
        foreach (NativeBattleModuleSound sound in step.Sounds)
        {
            PublishNativeModuleSound(sound);
        }
        NativeEffectMemory = step.Frame.Memory;
        return graphics with { OriginalEffect = step.Frame.Effect with { Curaga = step.Frame.State } };

        NativeBattleCuragaFrame ApplyContact(NativeBattleCuragaFrame current, int index)
        {
            NativeEffectMemory = current.Memory;
            ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(index)] });
            graphics = graphics with { PendingContacts = pending };
            return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Curaga contact lost its canonical process memory.") };
        }

        NativeBattleCuragaFrame AdvanceNodes(NativeBattleCuragaFrame current, uint address)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Curaga requires its initialized original OT.");
            NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native Curaga requires its55CBD2 random owner.");
            NativeBattleCuragaNodePresentationStep nodes = NativeBattleCuragaNodeKinematics.AdvanceAndDraw87EC80(current, address, random, inputs.ReadModel, projection, NativeCamera!.Roll, primitive.Pending, definition.NativeGraphics!.Rules.SummonColors);
            nativeCrtRandom = nodes.Random;
            NativeEffectMemory = nodes.Frame.Memory;
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = nodes.Ordering } };
            return nodes.Frame;
        }

        NativeBattleCuragaFrame Execute(NativeBattleCuragaFrame current, NativeBattleCuragaOperation operation)
        {
            NativeEffectMemory = current.Memory;
            switch (operation)
            {
                case NativeBattleCuragaStageFadeOperation fade:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Curaga fades require the retained four-group stage owner.");
                    if (stage.Groups.IsDefault || stage.Groups.Length != 4)
                    {
                        throw new InvalidDataException("Native Curaga fades require all four original stage groups.");
                    }
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = fade.Fade, ColorFlags = fade.ClearRgb ? group.ColorFlags & 0xFF000000u : group.ColorFlags }).ToImmutableArray() };
                    break;
                case NativeBattleCuragaPacketOperation packets:
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Curaga flash requires its initialized original OT.");
                    int cursor = current.State.PrimaryDrawCursor ?? throw new NotSupportedException("Native Curaga flash requires87DC50's actual primary cursor.");
                    NativeBattleEffectPacketWrite write = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, packets.Batch, NativeBattleEffectGouraudWrites.FullDword);
                    NativeEffectMemory = write.Memory;
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = write.Ordering } };
                    current = current with { State = current.State with { PrimaryDrawCursor = write.Cursor } };
                    break;
                default:
                    throw new NotSupportedException("Native Curaga operation requires its original immediate owner.");
            }
            return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Curaga operation lost canonical memory.") };
        }
    }
}
