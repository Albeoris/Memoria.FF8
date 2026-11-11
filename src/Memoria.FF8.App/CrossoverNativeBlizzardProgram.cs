using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverNativeBlizzardState(NativeBattleBlizzardFrame Frame, NativeBattleActionPacketsState Packets, NativeBattleLogicalOrdering Ordering, int ViewTranslationY, bool ContactPublished)
{
    public short PendingViewTranslationY { get; init; }
    public bool Complete => Frame.Effect.ReturnedDescriptor == 0;
}
internal sealed record CrossoverNativeBlizzardStep(CrossoverNativeBlizzardState State, bool Contact, ImmutableArray<NativeBattleModuleSound> Sounds);

/// <summary>Original Blizzard task scheduler and packet producers within the host callback.</summary>
internal sealed class CrossoverNativeBlizzardProgram(GameAssets game)
{
    private readonly NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors!;

    public CrossoverNativeBlizzardState Begin(NativeBattleContactHalf contact, NativeCrtRandomState random, NativeBattleEffectTextureState textures)
    {
        if (contact.ActorIndex != 3)
        {
            throw new InvalidDataException("Mixed Blizzard requires one bound host enemy contact.");
        }
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[640], new byte[1176]);
        (packets, _) = packets.Prepare(new(0, 2, 11, -1, 4, 144, null));
        (packets, _) = packets.AppendContact(new(contact, new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(144, game.ReadNativeEffectCatalogForEffects([144]));
        selected = selected with { Parameters = new(0, 0, 8, 0, 0, 0) };
        (NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect) = NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), selected);
        NativeBattleBlizzardResources resources = effect.Binding.BlizzardResources!;
        textures = textures.Register5099A0(resources.Animation);
        int texture = effect.ResourcePointers.Single(pointer => pointer.GlobalAddress == 0x24BECB8).ArenaOffset;
        textures = textures.EnqueueArenaTim505E30(memory.Arena!, texture).State;
        textures = textures.Drain505D20(memory.Arena).State;
        return new(new(memory with { Textures = textures }, NativeBattleBlizzardController.Initialize61ABA0(effect, packets), random), packets, NativeBattleLogicalOrdering.Clear(), 0, false);
    }

    public CrossoverNativeBlizzardStep Advance(CrossoverNativeBlizzardState source, Func<NativeBattleVector> readTargetBone, Func<NativeBattleModelTransform> readTargetTransform, NativeBattleCameraProjection projection)
    {
        if (source.Complete)
        {
            return new(source, false, []);
        }
        projection = projection with { View = projection.View! with { Y = unchecked(projection.View.Y + source.PendingViewTranslationY) } };
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        NativeBattleBlizzardResources resources = source.Frame.Effect.Binding.BlizzardResources!;
        NativeBattleActionMatrix view = projection.View!;
        bool contact = false;
        short translation = 0;
        ImmutableArray<NativeBattleModuleSound>.Builder sounds = ImmutableArray.CreateBuilder<NativeBattleModuleSound>();
        NativeBattleBlizzardFrame frame = NativeBattleBlizzardController.Advance61BF70(source.Frame, new(0, source.Packets, actor => actor == 3 ? readTargetBone() : throw new InvalidDataException("Blizzard queried an unbound native target bone."), actor => actor == 3 ? readTargetTransform() : throw new InvalidDataException("Blizzard queried an unbound native transform."), Execute));
        return new(source with { Frame = frame, Ordering = ordering, ViewTranslationY = source.PendingViewTranslationY, PendingViewTranslationY = translation, ContactPublished = source.ContactPublished || contact }, contact, sounds.ToImmutable());

        NativeBattleBlizzardFrame Execute(NativeBattleBlizzardFrame current, NativeBattleBlizzardOperation operation)
        {
            switch (operation)
            {
                case NativeBattleBlizzardContact published:
                    if (published.Contact != 0 || contact || source.ContactPublished)
                    {
                        throw new InvalidDataException("Mixed Blizzard emitted a repeated or foreign contact.");
                    }
                    contact = true;
                    return current;
                case NativeBattleBlizzardSound sound when sound.Model == 3:
                    NativeBattleVector position = readTargetBone();
                    sounds.Add(new(resources.Sound, 0x100, new(position.X, position.Y, position.Z, 0)));
                    return current;
                case NativeBattleBlizzardCameraImpulse impulse:
                    translation = unchecked((short)(translation + impulse.Delta));
                    return current;
                case NativeBattleBlizzardDraw draw:
                    NativeBattleModulePoolTask task = draw.Task;
                    short phase = Word(task, 12);
                    if (task.Callback == 0x61B740)
                    {
                        NativeBattleEffectMemory moved = NativeBattleBlizzardFaceMotion.Draw61B9A0(current.Memory, task, resources, 0, view, projection, colors, batch => current = Append(current, batch));
                        return current with { Memory = current.Memory with { BlizzardFaces = moved.BlizzardFaces } };
                    }
                    if (task.Callback == 0x61B3A0)
                    {
                        short scale = Word(task, 28);
                        NativeBattleActionMatrix world = NativeBattleBlizzardFaceMotion.World(task, new(0, 0, 0), scale, 4096, scale);
                        NativeBattleActionMatrix matrix = NativeBattleBlizzardFaceMotion.Compose(view, world);
                        NativeBattleGfGeometryContext context = new(phase >= 8 ? 0xC3u : 3u, 0, phase >= 8 ? (phase - 8) * 682 : 0, null, null, 0, 17, 2);
                        return Append(current, NativeBattleGfGeometryPackets.EmitBatch7043B0(resources.BurstMesh.Bytes, 0, default, matrix, projection, context, colors, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(144, task, unchecked((int)task.Callback))));
                    }
                    bool five = task.Callback == 0x61B660;
                    short spriteScale = Word(task, task.Callback == 0x61B4F0 ? 32 : 28);
                    int spritePhase = five ? Math.Min(phase, (short)4) : phase;
                    NativeBattleModuleResource resource = five ? resources.Sprite5 : resources.Sprite16;
                    uint color = five && phase >= 4 ? unchecked((uint)(byte)(phase * 224)) * 0x10101u : 0x808080;
                    NativeBattleModuleDraw sprite = new(NativeBattleModuleDrawKind.Sprite, 0)
                    {
                        PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(144, task, unchecked((int)task.Callback)),
                        Resource = resource,
                        SpriteFrame = resource.SpriteFrame(spritePhase),
                        Phase = unchecked((ushort)spritePhase),
                        Anchor = new(Word(task, 16), Word(task, 18), Word(task, 20), Word(task, 22)),
                        Scale = spriteScale,
                        DepthOffset = -(spriteScale >> (five ? 5 : 3)),
                        Direction = 4096,
                        SpriteFlags = five ? (ushort)(phase >= 4 ? 12 : 8) : (ushort)0,
                        SpriteColor = color
                    };
                    NativeBattleModulePacketBatch sprites = NativeBattleModulePackets.EmitSprite571DC0(sprite, projection, 0, 17, 2);
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset!.Value, ordering, sprites, NativeBattleEffectGouraudWrites.FullDword);
                    ordering = written.Ordering;
                    return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor } };
                default:
                    throw new NotSupportedException("Mixed Blizzard requires its original bound operation.");
            }
        }

        NativeBattleBlizzardFrame Append(NativeBattleBlizzardFrame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset!.Value, ordering, batch);
            ordering = written.Ordering;
            return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor } };
        }
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));

    public static CrossoverNativeBlizzardState Capture(CrossoverNativeBlizzardState source) => source with { Frame = NativeBattleEffectComponentStorage.Capture(source.Frame), Ordering = NativeBattleEffectComponentStorage.Capture(source.Ordering) };
    public static CrossoverNativeBlizzardState Restore(CrossoverNativeBlizzardState source) => source with { Frame = NativeBattleEffectComponentStorage.Restore(source.Frame), Ordering = NativeBattleEffectComponentStorage.Restore(source.Ordering) };
}
