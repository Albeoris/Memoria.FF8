using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeFira61CE10(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleFiraResources resources = effect.Binding.FiraResources ?? throw new NotSupportedException("Native61CE10 requires its original Fira resources.");
        if ((effect.Parameters!.Flags79 & 1) == 0)
        {
            NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native61CE10 requires retained effect memory.");
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native61CE10 requires its texture queue.");
            textures = textures.Register5099A0(resources.Animation);
            int source = effect.ResourcePointers.Single(pointer => pointer.GlobalAddress == 0x24C10A8).ArenaOffset;
            textures = textures.EnqueueArenaTim505E30(memory.Arena!, source).State;
            NativeEffectMemory = memory with { Textures = textures };
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native61CE10 requires its camera process.")).RegisterControlResource(textures.Animation);
        }
        return graphics with { OriginalEffect = NativeBattleFiraController.Initialize61CE10(effect, nativeActionPackets) };
    }

    private NativeBattleGraphicsState AdvanceNativeFira61DD90(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleFiraResources resources = effect.Binding.FiraResources ?? throw new NotSupportedException("Native Fira requires its original executable resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Fira requires its published projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Fira requires its retained view matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Fira requires its depth-cue tables.");
        NativeBattleModuleResource common5 = definition.NativeModuleResources.Single(resource => resource.Id == 5);
        NativeBattleFiraFrame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Fira requires retained effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Fira requires its retained CRT random state."));
        frame = NativeBattleFiraController.Advance61DD90(frame, new(graphics.Flags, nativeActionPackets, ReadPosition, Execute));
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleFiraPosition ReadPosition(int model)
        {
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, model);
            NativeBattleModelTransform transform = graphics.Models[model].Transform ?? throw new NotSupportedException("Native571400 requires its actual graphics transform row.");
            return new(anchor.X, anchor.Y, anchor.Z, transform.Angle36);
        }

        NativeBattleFiraFrame Execute(NativeBattleFiraFrame current, NativeBattleFiraOperation operation)
        {
            NativeEffectMemory = current.Memory;
            switch (operation)
            {
                case NativeBattleFiraContact contact:
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Contact)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleFiraSound sound:
                    NativeBattleFiraPosition position = ReadPosition(sound.Owner);
                    PublishNativeModuleSound(resources.Sound, 0x100, new(position.X, position.Y, position.Z, position.Fourth));
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleFiraStage stage:
                    NativeBattleStageState nativeStage = NativeStage ?? throw new NotSupportedException("Native5713E0 requires its retained stage rows.");
                    NativeStage = nativeStage with { Groups = nativeStage.Groups.Select(group => group with { ColorDepth = stage.Value, ColorFlags = 0 }).ToImmutableArray() };
                    return current;
                case NativeBattleFiraCommonTextureLoad load:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)load.DestinationArenaOffset), load.CompletionAddress, (address, value) => current = current with { Effect = NativeBattleFiraController.WriteCompletionByte(current.Effect, address, value) });
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleFiraDraw draw:
                    NativeBattleFiraDrawResult result = NativeBattleFiraDrawing.Draw(draw.Task, resources, common5, projection, view, NativeCamera!.Roll, colors);
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Fira requires its initialized ordering table.");
                    NativeBattleEffectPacketWrite written = result.Sprites is { } sprites
                        ? NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset!.Value, primitive.Pending, sprites, NativeBattleEffectGouraudWrites.FullDword)
                        : result.Mesh is { } mesh
                            ? NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset!.Value, primitive.Pending, mesh)
                            : throw new InvalidDataException("Native Fira draw callback emitted no drawable resource.");
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor } };
                default:
                    throw new NotSupportedException("Native Fira operation requires its original callback.");
            }
        }
    }

    private bool TryWriteNativeFiraCompletion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        if (address < 0x24BFB48 || address >= 0x24BFB68 || (address - 0x24BFB48) % 16 != 14)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: 0x61CE10 } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = NativeBattleFiraController.WriteCompletionByte(detached, address, value) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: 0x61CE10 } current ? current : throw new NotSupportedException("Native508660 completion lost its physical Fira task owner.");
            updated = graphics with { OriginalEffect = NativeBattleFiraController.WriteCompletionByte(effect, address, value) };
        }
        return true;
    }
}
