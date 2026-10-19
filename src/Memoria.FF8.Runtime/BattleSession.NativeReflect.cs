using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeReflect(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Reflect requires its selected effect.");
        NativeBattleReflectResources resources = effect.Binding.ReflectResources ?? throw new NotSupportedException("Native Reflect requires its original resources.");
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native Reflect initialization requires its retained CRT random owner.");
        nativeCrtRandom = random.Draw(out ushort seed);
        NativeBattleReflectFrame frame = NativeBattleReflectController.Initialize(NativeEffectMemory ?? throw new NotSupportedException("Native Reflect requires its current process memory."), effect, resources, seed);
        NativeEffectMemory = frame.Memory;
        if ((effect.Parameters!.Flags79 & 1) == 0)
        {
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Reflect requires its camera owner.")).RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeReflect(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Reflect requires its selected effect.");
        NativeBattleReflectResources resources = effect.Binding.ReflectResources ?? throw new NotSupportedException("Native Reflect requires its original resources.");
        NativeBattleReflectFrame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Reflect requires retained memory."), effect);
        NativeBattleReflectInputs inputs = new(nativeActionPackets, NativeCamera?.Projection ?? throw new NotSupportedException("Native Reflect requires its current projection."), definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Reflect requires its original interpolation constants."), index => graphics.Models[index], index => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Reflect requires its current target pose."), Emit) { ApplyContact = Contact };
        NativeBattleReflectStep step = NativeBattleReflectController.Advance(frame, resources, inputs);
        NativeEffectMemory = step.Frame.Memory;
        foreach (NativeBattleModuleSound sound in step.Sounds)
        {
            PublishNativeModuleSound(sound);
        }
        return graphics with { OriginalEffect = step.Frame.Effect };

        NativeBattleReflectFrame Contact(NativeBattleReflectFrame current, int index)
        {
            NativeEffectMemory = current.Memory;
            ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(index)] });
            graphics = graphics with { PendingContacts = pending };
            return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Reflect contact lost its process memory.") };
        }

        NativeBattleReflectFrame Emit(NativeBattleReflectFrame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattleReflectState state = current.Memory.Reflect106 ?? throw new NotSupportedException("Native Reflect lost its executable packet bank.");
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Reflect requires its pending ordering table.");
            NativeBattleRegionalPacketWrite written = NativeBattleRegionalPacketArena.Append(state.Packets, "2523C98", state.Cursor ?? throw new NotSupportedException("Native Reflect lost its current packet cursor."), 0x20000, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = current.Memory with { Reflect106 = state with { Packets = written.Arena, Cursor = written.Cursor } } };
        }
    }
}
