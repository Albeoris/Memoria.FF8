namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeClash26(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native8885D0 requires its selected original module.");
        _ = effect.Binding.Clash26Resources ?? throw new NotSupportedException("Native8885D0 requires its authenticated executable resources.");
        NativeBattleClash26Frame frame = NativeBattleClash26Controller.Initialize8885D0(NativeEffectMemory ?? throw new NotSupportedException("Native8885D0 requires its retained process memory."), effect, nativeActionPackets);
        NativeEffectMemory = frame.Memory;
        return graphics with { OriginalEffect = frame.Effect with { Clash26 = frame.State } };
    }

    private NativeBattleGraphicsState AdvanceNativeClash26(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native8886C0 requires its current original module.");
        NativeBattleClash26Resources resources = effect.Binding.Clash26Resources ?? throw new NotSupportedException("Native8886C0 requires its authenticated executable resources.");
        NativeBattleClash26State state = effect.Clash26 ?? throw new NotSupportedException("Native8886C0 requires its executed8885D0 state.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native8886C0 requires its actual projection owner.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native888A50 requires its original GTE color constants.");
        NativeBattleClash26Inputs inputs = new(nativeActionPackets, projection, NativeCamera!.Roll, colors, index => graphics.Models[index], index => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Clash anchors require the actual body rig."), Execute);
        NativeBattleClash26Step step = NativeBattleClash26Controller.Advance8886C0(new(NativeEffectMemory ?? throw new NotSupportedException("Native8886C0 requires its process memory."), effect, state), resources, inputs, nativeCrtRandom ?? throw new NotSupportedException("Native Clash requires its actual CRT random owner."));
        nativeCrtRandom = step.Random;
        NativeEffectMemory = step.Frame.Memory;
        return graphics with { OriginalEffect = step.Frame.Effect with { Clash26 = step.Frame.State } };

        NativeBattleClash26Frame Execute(NativeBattleClash26Frame current, NativeBattleClash26Operation operation)
        {
            NativeEffectMemory = current.Memory;
            switch (operation)
            {
                case NativeBattleClash26Model model:
                    graphics = graphics with { Models = graphics.Models.SetItem(model.Model.Index, model.Model) };
                    break;
                case NativeBattleClash26Sequence sequence:
                    graphics = SelectNativeHeaderSequence(graphics, sequence.Actor, sequence.Sequence);
                    break;
                case NativeBattleClash26Sound sound:
                    PublishNativeModuleSound(new(sound.Sound, 0, null) { Pan = 128 });
                    break;
                case NativeBattleClash26Contact contact:
                    var pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    break;
                case NativeBattleClash26Packets packets:
                    current = Append(current, (memory, cursor, ordering) => NativeBattleEffectPacketArena.Append(memory, cursor, ordering, packets.Batch));
                    break;
                case NativeBattleClash26Sprite sprite:
                    current = Append(current, (memory, cursor, ordering) => NativeBattleEffectPacketArena.Append(memory, cursor, ordering, sprite.Batch, NativeBattleEffectGouraudWrites.FullDword));
                    break;
                default:
                    throw new NotSupportedException("Native Clash operation requires its original immediate owner.");
            }
            return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Clash lost its current process memory.") };
        }

        NativeBattleClash26Frame Append(NativeBattleClash26Frame current, Func<NativeBattleEffectMemory, int, NativeBattleLogicalOrdering, NativeBattleEffectPacketWrite> write)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Clash requires its initialized physical OT.");
            int cursor = current.State.DrawCursor ?? throw new NotSupportedException("Native Clash packets require its current8886C0 arena selection.");
            NativeBattleEffectPacketWrite written = write(current.Memory, cursor, primitive.Pending);
            NativeEffectMemory = written.Memory;
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State with { DrawCursor = written.Cursor } };
        }
    }
}
