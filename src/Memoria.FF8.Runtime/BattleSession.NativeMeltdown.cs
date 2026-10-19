using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeMeltdown(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Meltdown requires its selected effect.");
        NativeBattleMeltdownResources resources = effect.Binding.MeltdownResources ?? throw new NotSupportedException("Native Meltdown requires its original resources.");
        NativeBattleMeltdownFrame frame = NativeBattleMeltdownController.Initialize(NativeEffectMemory ?? throw new NotSupportedException("Native Meltdown requires retained memory."), effect, nativeActionPackets, resources, nativeCrtRandom ?? throw new NotSupportedException("Native Meltdown requires its CRT random owner."));
        NativeEffectMemory = frame.Memory;
        if ((effect.Parameters!.Flags79 & 1) == 0)
        {
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Meltdown requires its camera owner.")).RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeMeltdown(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Meltdown requires its selected effect.");
        NativeBattleMeltdownResources resources = effect.Binding.MeltdownResources ?? throw new NotSupportedException("Native Meltdown requires its original resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Meltdown requires its current projection.");
        NativeBattleMeltdownFrame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Meltdown requires retained memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Meltdown requires its CRT random owner."));
        NativeBattleMeltdownInputs inputs = new(nativeActionPackets, projection, definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Meltdown requires its original interpolation constants."), (graphics.Flags & 0x201) != 0, index => graphics.Models[index], index => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Meltdown requires its current actor pose."), Execute);
        NativeBattleMeltdownFrame advanced = NativeBattleMeltdownController.Advance(frame, resources, inputs);
        NativeEffectMemory = advanced.Memory;
        nativeCrtRandom = advanced.Random;
        return graphics with { OriginalEffect = advanced.Effect };

        NativeBattleMeltdownFrame Execute(NativeBattleMeltdownFrame current, NativeBattleMeltdownOperation operation)
        {
            if (operation is NativeBattleMeltdownFade)
            {
                graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(0, 1, 8, 255) };
                return current;
            }
            if (operation is NativeBattleMeltdownSound sound)
            {
                NativeEffectMemory = current.Memory;
                PublishNativeModuleSound(new(sound.Sound, 0, null) { Pan = 128 });
                return current with { Memory = NativeEffectMemory! };
            }
            if (operation is NativeBattleMeltdownContact contact)
            {
                NativeEffectMemory = current.Memory;
                ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                graphics = graphics with { PendingContacts = pending };
                return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Meltdown contact lost process memory.") };
            }
            if (operation is NativeBattleMeltdownFlags flags)
            {
                graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, graphics.Models[flags.Actor] with { Flags0 = flags.Flags }) };
                return current;
            }
            if (operation is NativeBattleMeltdownActor actor)
            {
                (current, graphics) = DrawNativeMeltdownActor(current, graphics, actor);
                return current;
            }
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Meltdown requires its actual pending ordering table.");
            NativeBattleMeltdownState state = current.Memory.Meltdown148 ?? throw new NotSupportedException("Native Meltdown lost its packet arena owner.");
            int cursor = state.Cursor ?? throw new NotSupportedException("Native Meltdown lost its executed arena selection.");
            NativeBattleGfGeometryBatch batch = operation switch
            {
                NativeBattleMeltdownPackets packets => packets.Batch,
                NativeBattleMeltdownSprite sprite => NativeBattleMeltdownPacketConversion.FromSprite(sprite.Batch),
                _ => throw new NotSupportedException("Native Meltdown operation has no proved implementation.")
            };
            NativeBattleRegionalPacketWrite written = NativeBattleRegionalPacketArena.Append(state.Packets, "2466638", cursor, 0x10000, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = current.Memory with { Meltdown148 = state with { Packets = written.Arena, Cursor = written.Cursor } } };
        }
    }
}
