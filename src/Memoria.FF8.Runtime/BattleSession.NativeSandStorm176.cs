using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeSandStorm176(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Sand Storm requires its selected original effect.");
        NativeBattleSandStorm176Resources resources = effect.Binding.SandStorm176Resources ?? throw new NotSupportedException("Native Sand Storm requires its original resources.");
        NativeBattleSandStorm176Frame frame = NativeBattleSandStorm176Controller.Initialize(NativeEffectMemory ?? throw new NotSupportedException("Native Sand Storm requires its process memory."), effect, resources, nativeCrtRandom ?? throw new NotSupportedException("Native Sand Storm requires its shared CRT stream."));
        NativeEffectMemory = frame.Memory;
        NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Sand Storm requires its camera owner.")).RegisterControlResource(frame.Memory.Textures!.Animation);
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeSandStorm176(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Sand Storm requires its selected effect.");
        NativeBattleSandStorm176Resources resources = effect.Binding.SandStorm176Resources ?? throw new NotSupportedException("Native Sand Storm requires its original resources.");
        NativeBattleSandStorm176Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Sand Storm requires its process memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Sand Storm requires its shared CRT stream."));
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Sand Storm requires its camera owner.");
        NativeBattleSandStorm176Inputs inputs = new(nativeActionPackets, camera.Projection ?? throw new NotSupportedException("Native Sand Storm requires its published projection."), camera.Roll, (graphics.Flags & 0x201) != 0, index => graphics.Models[index], index => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Sand Storm requires its selected original skeleton."), Execute);
        frame = NativeBattleSandStorm176Controller.Advance(frame, resources, inputs);
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleSandStorm176Frame Execute(NativeBattleSandStorm176Frame current, NativeBattleSandStorm176Operation operation)
        {
            switch (operation)
            {
                case NativeBattleSandStorm176Sound sound:
                    PublishNativeModuleSound(new(sound.Sound, 0, null) { Pan = 128 });
                    return current;
                case NativeBattleSandStorm176Actor actor:
                    graphics = graphics with { Models = graphics.Models.SetItem(actor.Index, actor.Model) };
                    return current;
                case NativeBattleSandStorm176Contact contact:
                    NativeEffectMemory = current.Memory;
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Sand Storm contact lost its process owner.") };
                case NativeBattleSandStorm176Funnel funnel:
                    NativeBattleSandStorm176State state = current.Memory.SandStorm176 ?? throw new NotSupportedException("Native Sand Storm lost its executable packet bank.");
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Sand Storm requires its pending ordering table.");
                    NativeBattleRegionalPacketWrite written = NativeBattleSandStorm176Packets.WriteFunnel(state.Module, primitive.Pending, funnel.Batch);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return current with { Memory = current.Memory with { SandStorm176 = state with { Module = written.Arena } } };
                case NativeBattleSandStorm176Sprite sprite:
                    NativeBattlePrimitiveFrame spritePrimitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Sand Storm requires its sprite ordering table.");
                    NativeBattleEffectPacketWrite spriteWrite = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native Sand Storm lost its parity cursor."), spritePrimitive.Pending, NativeBattleMeltdownPacketConversion.FromSprite(sprite.Batch));
                    graphics = graphics with { PrimitiveFrame = spritePrimitive with { Pending = spriteWrite.Ordering } };
                    return current with { Memory = spriteWrite.Memory, Effect = current.Effect with { DrawArenaOffset = spriteWrite.Cursor } };
                default:
                    throw new NotSupportedException("Native Sand Storm emitted an unproved operation.");
            }
        }
    }
}
