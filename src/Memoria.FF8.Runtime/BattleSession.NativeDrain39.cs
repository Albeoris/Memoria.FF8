using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeDrain39(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native8513D0 requires its selected original effect.");
        NativeBattleDrain39Resources resources = effect.Binding.Drain39Resources ?? throw new NotSupportedException("Native8513D0 requires its authenticated executable resources.");
        NativeBattleDrain39Frame frame = NativeBattleDrain39Controller.Initialize8513D0(NativeEffectMemory ?? throw new NotSupportedException("Native8513D0 requires retained process memory."), effect, nativeActionPackets, resources);
        NativeEffectMemory = frame.Memory;
        if ((effect.Parameters!.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Native8513D0 requires its5099A0 animation registration.");
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native8513D0 requires the original camera owner.")).RegisterControlResource(textures.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect with { Drain39 = frame.State } };
    }

    private NativeBattleGraphicsState AdvanceNativeDrain39(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native851510 requires its original effect owner.");
        NativeBattleDrain39Resources resources = effect.Binding.Drain39Resources ?? throw new NotSupportedException("Native851510 requires its authenticated resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native851510 requires its prior projection registers.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native851510 requires the previously published1D97778 matrix.");
        NativeBattleDrain39Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native851510 requires retained effect memory."), effect, effect.Drain39 ?? throw new NotSupportedException("Native851510 requires its executed8513D0 state."));
        NativeBattleDrain39ControllerInputs inputs = new(nativeActionPackets, view, index => graphics.Models[index], index => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Drain39 anchors require the original model pose source.")) { ApplyContact = ApplyContact };
        NativeBattleDrain39ControllerStep step = NativeBattleDrain39Controller.Advance851510(frame, inputs, resources, (current, address) =>
        {
            NativeEffectMemory = current.Memory;
            NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
            NativeBattleDrain39LeafStep leaf;
            if (task.Callback == 0x851A80)
            {
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Drain requires its initialized ordering table.");
                NativeBattleDrain39NodeStep nodes = NativeBattleDrain39Nodes.Advance(current, address, resources, inputs, nativeCrtRandom ?? throw new NotSupportedException("Native Drain requires its shared CRT stream."), projection, NativeCamera!.Roll, primitive.Pending, definition.NativeGraphics!.Rules.SummonColors!);
                nativeCrtRandom = nodes.Random;
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = nodes.Ordering } };
                leaf = new(nodes.Frame, nodes.Result);
            }
            else
            {
                leaf = NativeBattleDrain39Simple.Advance(current, address, resources, Execute);
            }
            NativeEffectMemory = leaf.Frame.Memory;
            return leaf;
        });
        foreach (NativeBattleModuleSound sound in step.Sounds)
        {
            PublishNativeModuleSound(sound);
        }
        NativeEffectMemory = step.Frame.Memory;
        return graphics with { OriginalEffect = step.Frame.Effect with { Drain39 = step.Frame.State } };

        NativeBattleDrain39Frame ApplyContact(NativeBattleDrain39Frame current, int index)
        {
            NativeEffectMemory = current.Memory;
            ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(index)] });
            graphics = graphics with { PendingContacts = pending };
            return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Drain39 contact lost its canonical process memory.") };
        }

        NativeBattleDrain39Frame Execute(NativeBattleDrain39Frame current, NativeBattleDrain39Operation operation)
        {
            NativeEffectMemory = current.Memory;
            switch (operation)
            {
                case NativeBattleDrain39StageFadeOperation fade:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Drain39 fades require the retained four-group stage owner.");
                    if (stage.Groups.IsDefault || stage.Groups.Length != 4)
                    {
                        throw new InvalidDataException("Native Drain39 fades require all four original stage groups.");
                    }
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = fade.Fade, ColorFlags = fade.ClearRgb ? group.ColorFlags & 0xFF000000u : group.ColorFlags }).ToImmutableArray() };
                    break;
                case NativeBattleDrain39PacketOperation packets:
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Drain39 flash requires its initialized original OT.");
                    int cursor = current.State.PrimaryDrawCursor ?? throw new NotSupportedException("Native Drain39 flash requires851510's actual primary cursor.");
                    NativeBattleEffectPacketWrite write = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, packets.Batch, NativeBattleEffectGouraudWrites.FullDword);
                    NativeEffectMemory = write.Memory;
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = write.Ordering } };
                    current = current with { State = current.State with { PrimaryDrawCursor = write.Cursor } };
                    break;
                default:
                    throw new NotSupportedException("Native Drain39 operation requires its original immediate owner.");
            }
            return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Drain39 operation lost canonical memory.") };
        }
    }
}
