using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeBlizzard61ABA0(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleBlizzardResources resources = effect.Binding.BlizzardResources ?? throw new NotSupportedException("Native61ABA0 requires its original Blizzard resources.");
        if ((effect.Parameters!.Flags79 & 1) == 0)
        {
            NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native61ABA0 requires its571870 memory.");
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native61ABA0 requires its texture queue.");
            textures = textures.Register5099A0(resources.Animation);
            int source = effect.ResourcePointers.Single(pointer => pointer.GlobalAddress == 0x24BECB8).ArenaOffset;
            textures = textures.EnqueueArenaTim505E30(memory.Arena!, source).State;
            NativeEffectMemory = memory with { Textures = textures };
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native61ABA0 requires its original camera state.")).RegisterControlResource(textures.Animation);
        }
        return graphics with { OriginalEffect = NativeBattleBlizzardController.Initialize61ABA0(effect, nativeActionPackets) };
    }

    private NativeBattleGraphicsState AdvanceNativeBlizzard61BF70(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleBlizzardResources resources = effect.Binding.BlizzardResources ?? throw new NotSupportedException("Native Blizzard requires its executable resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Blizzard requires the prior published projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Blizzard requires the prior1D97778 view matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Blizzard requires its authenticated depth-cue color tables.");
        NativeBattleBlizzardFrame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Blizzard requires retained effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Blizzard requires its retained55CBD2 CRT state."));
        frame = NativeBattleBlizzardController.Advance61BF70(frame, new(graphics.Flags, nativeActionPackets, index => ReadNativeGraphicsBone(graphics, index, 240, 4096), index => graphics.Models[index].Transform ?? throw new NotSupportedException("Native Blizzard requires its target WORD36."), Execute));
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleBlizzardFrame Execute(NativeBattleBlizzardFrame current, NativeBattleBlizzardOperation operation)
        {
            NativeEffectMemory = current.Memory;
            switch (operation)
            {
                case NativeBattleBlizzardContact contact:
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Contact)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleBlizzardSound sound:
                    PublishNativeModuleSound(resources.Sound, 0x100, ReadNativeModelAnchor(graphics, sound.Model));
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleBlizzardCameraImpulse impulse:
                    NativeCamera = NativeCamera!.AddViewTranslationY61B8D5(impulse.Delta);
                    return current;
                case NativeBattleBlizzardDraw draw:
                    NativeBattleModulePoolTask task = draw.Task;
                    short phase = NativeBattleBlizzardController.Word(task, 12);
                    if (task.Callback == 0x61B740)
                    {
                        NativeBattleEffectMemory moved = NativeBattleBlizzardFaceMotion.Draw61B9A0(current.Memory, task, resources, graphics.Flags, view, projection, colors, batch => current = Append(current, batch));
                        return current with { Memory = current.Memory with { BlizzardFaces = moved.BlizzardFaces } };
                    }
                    if (task.Callback == 0x61B3A0)
                    {
                        short scale = NativeBattleBlizzardController.Word(task, 28);
                        NativeBattleActionMatrix world = NativeBattleBlizzardFaceMotion.World(task, new(0, 0, 0), scale, 4096, scale);
                        NativeBattleActionMatrix matrix = NativeBattleBlizzardFaceMotion.Compose(view, world);
                        NativeBattleGfGeometryContext context = new(phase >= 8 ? 0xC3u : 3u, 0, phase >= 8 ? (phase - 8) * 682 : 0, null, null, 0, 17, 2);
                        return Append(current, NativeBattleGfGeometryPackets.EmitBatch7043B0(resources.BurstMesh.Bytes, 0, default, matrix, projection, context, colors, pilotIdentity: new(144, 0x24BD788, task.PilotSlot, NativeBattlePilotVisualLifetimes.Generation(task, task.PilotSlot), unchecked((int)task.Callback))));
                    }
                    bool five = task.Callback == 0x61B660;
                    short spriteScale = NativeBattleBlizzardController.Word(task, task.Callback == 0x61B4F0 ? 32 : 28);
                    int spritePhase = five ? Math.Min(phase, (short)4) : phase;
                    NativeBattleModuleResource resource = five ? resources.Sprite5 : resources.Sprite16;
                    uint color = five && phase >= 4 ? unchecked((uint)(byte)(phase * 224)) * 0x10101u : 0x808080;
                    NativeBattleModuleDraw sprite = new(NativeBattleModuleDrawKind.Sprite, 0)
                    {
                        PilotIdentity = new(144, 0x24BD788, task.PilotSlot, NativeBattlePilotVisualLifetimes.Generation(task, task.PilotSlot), unchecked((int)task.Callback)),
                        Resource = resource,
                        SpriteFrame = resource.SpriteFrame(spritePhase),
                        Phase = unchecked((ushort)spritePhase),
                        Anchor = new(NativeBattleBlizzardController.Word(task, 16), NativeBattleBlizzardController.Word(task, 18), NativeBattleBlizzardController.Word(task, 20), NativeBattleBlizzardController.Word(task, 22)),
                        Scale = spriteScale,
                        DepthOffset = -(spriteScale >> (five ? 5 : 3)),
                        Direction = 4096,
                        SpriteFlags = five ? (ushort)(phase >= 4 ? 12 : 8) : (ushort)0,
                        SpriteColor = color
                    };
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Blizzard requires its original OT frame.");
                    NativeBattleModulePacketBatch sprites = NativeBattleModulePackets.EmitSprite571DC0(sprite, projection, NativeCamera!.Roll, 17, 2);
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset!.Value, primitive.Pending, sprites, NativeBattleEffectGouraudWrites.FullDword);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor } };
                default:
                    throw new NotSupportedException("Native Blizzard operation requires its original callback.");
            }
        }

        NativeBattleBlizzardFrame Append(NativeBattleBlizzardFrame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Blizzard requires its initialized OT.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset!.Value, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            NativeEffectMemory = written.Memory;
            return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor } };
        }
    }
}
