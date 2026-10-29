using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeDribble41(NativeBattleGraphicsState graphics) => EnterNativeWendigo(graphics, false);
    private NativeBattleGraphicsState EnterNativeShoot42(NativeBattleGraphicsState graphics) => EnterNativeWendigo(graphics, true);
    private NativeBattleGraphicsState AdvanceNativeDribble41(NativeBattleGraphicsState graphics) => AdvanceNativeWendigo(graphics, false);
    private NativeBattleGraphicsState AdvanceNativeShoot42(NativeBattleGraphicsState graphics) => AdvanceNativeWendigo(graphics, true);

    private NativeBattleGraphicsState EnterNativeWendigo(NativeBattleGraphicsState graphics, bool shoot)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Wendigo requires its selected original effect.");
        NativeBattleWendigoResources resources = (shoot ? effect.Binding.Shoot42Resources : effect.Binding.Dribble41Resources) ?? throw new NotSupportedException("Native Wendigo requires its original resources.");
        if (shoot)
        {
            NativeBattleShootParty.RequireBallOwner(graphics.Models);
        }
        NativeBattleWendigoFrame frame = NativeBattleWendigoController.Initialize(NativeEffectMemory ?? throw new NotSupportedException("Native Wendigo requires its process memory."), effect, nativeActionPackets, resources, nativeCrtRandom ?? throw new NotSupportedException("Native Wendigo requires its shared CRT stream."));
        NativeEffectMemory = frame.Memory;
        if ((effect.Parameters!.Flags79 & 1) == 0)
        {
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Wendigo requires its camera owner.")).RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeWendigo(NativeBattleGraphicsState graphics, bool shoot)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Wendigo requires its selected original effect.");
        NativeBattleWendigoResources resources = (shoot ? effect.Binding.Shoot42Resources : effect.Binding.Dribble41Resources) ?? throw new NotSupportedException("Native Wendigo requires its original resources.");
        NativeBattleWendigoFrame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Wendigo requires its process memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Wendigo requires its shared CRT stream."));
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Wendigo requires its camera owner.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Wendigo requires its published projection.");
        NativeBattleWendigoInputs inputs = new(nativeActionPackets, projection, camera.Roll, definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Wendigo requires its original interpolation constants."), index => graphics.Models[index], index => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Wendigo requires its current actor skeleton."), Execute);
        frame = NativeBattleWendigoController.Advance(frame, resources, inputs);
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleWendigoFrame Execute(NativeBattleWendigoFrame current, NativeBattleWendigoOperation operation)
        {
            switch (operation)
            {
                case NativeBattleWendigoSound sound:
                    NativeEffectMemory = current.Memory;
                    PublishNativeModuleSound(new(sound.Sound, sound.Flags, null) { Pan = 128 });
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Wendigo sound lost its process owner.") };
                case NativeBattleWendigoContact contact:
                    NativeEffectMemory = current.Memory;
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Wendigo contact lost its process owner.") };
                case NativeBattleWendigoModel model:
                    graphics = graphics with { Models = graphics.Models.SetItem(model.Model.Index, model.Model) };
                    return current;
                case NativeBattleWendigoSequence sequence:
                    graphics = SelectNativeHeaderSequence(graphics, sequence.Actor, sequence.Sequence);
                    return current;
                case NativeBattleWendigoDrawBall ball:
                    NativeBattleWendigoState ballOwner = Owner(current);
                    NativeEffectMemory = current.Memory;
                    NativeBattleEffectArena module;
                    (graphics, module) = DrawNativeWendigoBall(graphics, ball.Actor, ballOwner.Module, NativeBattleWendigoController.ModuleBase(shoot), NativeBattleWendigoController.BallAddress(shoot), shoot, ball.PilotIdentity);
                    NativeBattleEffectMemory ballMemory = NativeEffectMemory ?? throw new InvalidDataException("Native Wendigo renderer lost its frame cursor owner.");
                    return current with { Memory = shoot ? ballMemory with { Shoot42 = ballOwner with { Module = module } } : ballMemory with { Dribble41 = ballOwner with { Module = module } } };
                case NativeBattleWendigoPackets packets:
                    return Append(current, packets.Batch, packets.Secondary);
                case NativeBattleWendigoSprite sprite:
                    return Append(current, NativeBattleMeltdownPacketConversion.FromSprite(NativeBattleModulePackets.EmitSprite571DC0(sprite.Draw, projection, camera.Roll, 17, 2)), sprite.Secondary);
                default:
                    throw new NotSupportedException("Native Wendigo emitted an unproved operation.");
            }
        }
        NativeBattleWendigoState Owner(NativeBattleWendigoFrame current) => (shoot ? current.Memory.Shoot42 : current.Memory.Dribble41) ?? throw new NotSupportedException("Native Wendigo lost its executable owner.");
        NativeBattleWendigoFrame Append(NativeBattleWendigoFrame current, NativeBattleGfGeometryBatch batch, bool secondary)
        {
            NativeBattleWendigoState state = Owner(current);
            int cursor = (secondary ? state.SecondaryCursor : state.PrimaryCursor) ?? throw new NotSupportedException("Native Wendigo lost its selected file packet cursor.");
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Wendigo requires its pending ordering table.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            state = secondary ? state with { SecondaryCursor = written.Cursor } : state with { PrimaryCursor = written.Cursor };
            return current with { Memory = shoot ? written.Memory with { Shoot42 = state } : written.Memory with { Dribble41 = state } };
        }
    }
}
